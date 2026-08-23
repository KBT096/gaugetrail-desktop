namespace GaugeTrail.Core;

public static class StatisticalAnalyzer
{
    private const double MovingRangeD2 = 1.128;

    public static AnalysisResult Analyze(QualityWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var points = workspace.Measurements
            .Where(record => double.IsFinite(record.Value))
            .OrderBy(record => record.Timestamp)
            .ThenBy(record => record.Id, StringComparer.Ordinal)
            .ToArray();

        if (points.Length == 0)
        {
            return AnalysisResult.Empty;
        }

        var values = points.Select(point => point.Value).ToArray();
        var mean = values.Average();
        var median = Median(values);
        var overallSigma = SampleStandardDeviation(values);
        var movingRanges = values.Zip(values.Skip(1), (left, right) => Math.Abs(right - left)).ToArray();
        var withinSigma = movingRanges.Length == 0 ? 0 : movingRanges.Average() / MovingRangeD2;

        if (!double.IsFinite(withinSigma) || withinSigma <= 1e-12)
        {
            withinSigma = overallSigma;
        }

        var lcl = mean - 3 * withinSigma;
        var ucl = mean + 3 * withinSigma;
        var alerts = RuleEngine.Evaluate(points, mean, withinSigma);
        var cp = CapabilityPotential(workspace.LowerSpecLimit, workspace.UpperSpecLimit, withinSigma);
        var cpk = CapabilityCentered(workspace.LowerSpecLimit, workspace.UpperSpecLimit, mean, withinSigma);
        var pp = CapabilityPotential(workspace.LowerSpecLimit, workspace.UpperSpecLimit, overallSigma);
        var ppk = CapabilityCentered(workspace.LowerSpecLimit, workspace.UpperSpecLimit, mean, overallSigma);
        var (state, interpretation) = Interpret(points.Length, alerts, cpk);

        return new AnalysisResult
        {
            Count = points.Length,
            Mean = mean,
            Median = median,
            Minimum = values.Min(),
            Maximum = values.Max(),
            WithinSigma = withinSigma,
            OverallSigma = overallSigma,
            LowerControlLimit = lcl,
            UpperControlLimit = ucl,
            Cp = cp,
            Cpk = cpk,
            Pp = pp,
            Ppk = ppk,
            State = state,
            Interpretation = interpretation,
            OrderedMeasurements = points,
            Alerts = alerts
        };
    }

    public static double SampleStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var mean = values.Average();
        var sum = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(sum / (values.Count - 1));
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }

    private static double? CapabilityPotential(double? lower, double? upper, double sigma)
    {
        if (lower is null || upper is null || sigma <= 0 || upper <= lower)
        {
            return null;
        }

        return (upper.Value - lower.Value) / (6 * sigma);
    }

    private static double? CapabilityCentered(
        double? lower,
        double? upper,
        double mean,
        double sigma)
    {
        if (sigma <= 0)
        {
            return null;
        }

        var lowerSide = lower is null ? double.PositiveInfinity : (mean - lower.Value) / (3 * sigma);
        var upperSide = upper is null ? double.PositiveInfinity : (upper.Value - mean) / (3 * sigma);
        var result = Math.Min(lowerSide, upperSide);
        return double.IsInfinity(result) ? null : result;
    }

    private static (string State, string Interpretation) Interpret(
        int pointCount,
        IReadOnlyList<RuleAlert> alerts,
        double? cpk)
    {
        if (pointCount < 20)
        {
            return ("样本不足", "当前少于 20 个点，结果适合演示和初筛，不宜据此冻结控制限。");
        }

        if (alerts.Any(alert => alert.Severity == AlertSeverity.Critical))
        {
            return ("需立即检查", "发现越过 3σ 的点。请先确认量具、录入和特殊原因，再决定是否调整过程。");
        }

        if (alerts.Any(alert => alert.Severity == AlertSeverity.Warning))
        {
            return ("存在非随机信号", "检测到偏移、趋势或分布形态信号。系统只指出证据，不替代现场原因调查。");
        }

        if (cpk is < 1)
        {
            return ("能力不足", "当前 Cpk 低于 1，过程分布与规格之间余量不足。");
        }

        if (cpk is < 1.33)
        {
            return ("能力需关注", "当前 Cpk 介于 1.00 与 1.33 之间，建议结合风险和客户要求评估。");
        }

        return ("暂未发现异常", "在当前规则和数据范围内未发现非随机信号，仍需结合抽样方案与现场信息判断。");
    }
}
