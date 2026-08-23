namespace GaugeTrail.Core;

public static class RuleEngine
{
    public static IReadOnlyList<RuleAlert> Evaluate(
        IReadOnlyList<MeasurementRecord> points,
        double center,
        double sigma)
    {
        if (points.Count == 0 || !double.IsFinite(sigma) || sigma <= 0)
        {
            return [];
        }

        var alerts = new List<RuleAlert>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var z = points.Select(point => (point.Value - center) / sigma).ToArray();

        for (var i = 0; i < points.Count; i++)
        {
            if (Math.Abs(z[i]) > 3)
            {
                Add(
                    alerts,
                    keys,
                    points,
                    i,
                    "R1",
                    "单点越过 3σ",
                    AlertSeverity.Critical,
                    "该点已越过三倍过程内标准差控制线。",
                    $"标准化距离 {z[i]:0.00}σ；阈值为 ±3σ。");
            }
        }

        EvaluateWindows(points, z, 9, "R2", "连续 9 点位于中心线同侧", AlertSeverity.Warning,
            window => window.All(value => value > 0) || window.All(value => value < 0),
            "过程中心可能已经发生持续偏移。",
            "最近 9 点全部位于中心线同一侧。",
            alerts, keys);

        EvaluateWindows(points, points.Select(point => point.Value).ToArray(), 6, "R3", "连续 6 点单调变化",
            AlertSeverity.Warning,
            window => IsStrictlyMonotonic(window),
            "过程可能存在持续上升或下降趋势。",
            "最近 6 点呈严格单调变化。",
            alerts, keys);

        EvaluateWindows(points, points.Select(point => point.Value).ToArray(), 14, "R4", "连续 14 点交替升降",
            AlertSeverity.Notice,
            window => IsAlternating(window),
            "过程可能受到周期性切换或过度调节影响。",
            "最近 14 点的相邻变化方向持续交替。",
            alerts, keys);

        EvaluateWindows(points, z, 3, "R5", "3 点中有 2 点越过同侧 2σ", AlertSeverity.Warning,
            window => window.Count(value => value > 2) >= 2 || window.Count(value => value < -2) >= 2,
            "短窗口内出现同侧较大偏移。",
            "最近 3 点中至少 2 点位于同侧 2σ 之外。",
            alerts, keys);

        EvaluateWindows(points, z, 5, "R6", "5 点中有 4 点越过同侧 1σ", AlertSeverity.Warning,
            window => window.Count(value => value > 1) >= 4 || window.Count(value => value < -1) >= 4,
            "数据正在中心线一侧聚集。",
            "最近 5 点中至少 4 点位于同侧 1σ 之外。",
            alerts, keys);

        EvaluateWindows(points, z, 15, "R7", "连续 15 点落在 ±1σ 内", AlertSeverity.Notice,
            window => window.All(value => Math.Abs(value) < 1),
            "波动异常收窄，可能存在分层、取整或抽样方式变化。",
            "最近 15 点全部落在中心线 ±1σ 内。",
            alerts, keys);

        EvaluateWindows(points, z, 8, "R8", "连续 8 点落在 ±1σ 外", AlertSeverity.Warning,
            window => window.All(value => Math.Abs(value) > 1)
                      && window.Any(value => value > 0)
                      && window.Any(value => value < 0),
            "数据分布可能呈现混合、分层或中心区域缺失。",
            "最近 8 点均在 ±1σ 外，且分布于中心线两侧。",
            alerts, keys);

        return CoalesceOverlappingWindows(alerts)
            .OrderByDescending(alert => alert.Severity)
            .ThenByDescending(alert => alert.PointIndex)
            .ToArray();
    }

    private static IReadOnlyList<RuleAlert> CoalesceOverlappingWindows(IReadOnlyList<RuleAlert> alerts)
    {
        var result = new List<RuleAlert>();
        foreach (var group in alerts.GroupBy(alert => alert.RuleCode, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(alert => alert.PointIndex).ToArray();
            if (group.Key == "R1")
            {
                result.AddRange(ordered);
                continue;
            }

            RuleAlert? previous = null;
            foreach (var alert in ordered)
            {
                if (previous is null || alert.PointIndex > previous.PointIndex + 1)
                {
                    result.Add(alert);
                }

                previous = alert;
            }
        }

        return result;
    }

    private static void EvaluateWindows(
        IReadOnlyList<MeasurementRecord> points,
        IReadOnlyList<double> values,
        int width,
        string code,
        string name,
        AlertSeverity severity,
        Func<IReadOnlyList<double>, bool> predicate,
        string message,
        string evidence,
        List<RuleAlert> alerts,
        HashSet<string> keys)
    {
        for (var end = width - 1; end < values.Count; end++)
        {
            var window = values.Skip(end - width + 1).Take(width).ToArray();
            if (predicate(window))
            {
                Add(alerts, keys, points, end, code, name, severity, message, evidence);
            }
        }
    }

    private static bool IsStrictlyMonotonic(IReadOnlyList<double> values)
    {
        var increasing = true;
        var decreasing = true;
        for (var i = 1; i < values.Count; i++)
        {
            increasing &= values[i] > values[i - 1];
            decreasing &= values[i] < values[i - 1];
        }

        return increasing || decreasing;
    }

    private static bool IsAlternating(IReadOnlyList<double> values)
    {
        var previousDirection = 0;
        for (var i = 1; i < values.Count; i++)
        {
            var direction = Math.Sign(values[i] - values[i - 1]);
            if (direction == 0 || (previousDirection != 0 && direction == previousDirection))
            {
                return false;
            }

            previousDirection = direction;
        }

        return true;
    }

    private static void Add(
        List<RuleAlert> alerts,
        HashSet<string> keys,
        IReadOnlyList<MeasurementRecord> points,
        int index,
        string code,
        string name,
        AlertSeverity severity,
        string message,
        string evidence)
    {
        var point = points[index];
        var key = $"{code}:{point.Id}";
        if (!keys.Add(key))
        {
            return;
        }

        alerts.Add(new RuleAlert
        {
            Key = key,
            RuleCode = code,
            RuleName = name,
            Severity = severity,
            PointIndex = index,
            MeasurementId = point.Id,
            Timestamp = point.Timestamp,
            Value = point.Value,
            Message = message,
            Evidence = evidence
        });
    }
}
