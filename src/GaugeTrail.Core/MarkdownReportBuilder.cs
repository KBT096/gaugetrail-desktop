using System.Globalization;
using System.Text;

namespace GaugeTrail.Core;

public static class MarkdownReportBuilder
{
    public static string Build(QualityWorkspace workspace, AnalysisResult analysis, DateTime generatedAt)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(analysis);

        var unitSuffix = string.IsNullOrWhiteSpace(workspace.Unit) ? string.Empty : $" {workspace.Unit}";
        var builder = new StringBuilder();
        builder.AppendLine($"# {Escape(workspace.Name)} · 过程快照");
        builder.AppendLine();
        builder.AppendLine($"> 由 GaugeTrail Desktop 在 {generatedAt:yyyy-MM-dd HH:mm:ss zzz} 本地生成。");
        builder.AppendLine();
        builder.AppendLine("## 结论先行");
        builder.AppendLine();
        builder.AppendLine($"- 当前状态：**{analysis.State}**");
        builder.AppendLine($"- 解释：{analysis.Interpretation}");
        builder.AppendLine($"- 数据点：{analysis.Count}");
        builder.AppendLine($"- 规则信号：{analysis.Alerts.Count}");
        builder.AppendLine($"- 未关闭行动项：{workspace.Actions.Count(action => action.Status != ActionStatus.Closed)}");
        builder.AppendLine();
        builder.AppendLine("## 统计摘要");
        builder.AppendLine();
        builder.AppendLine("| 指标 | 结果 |");
        builder.AppendLine("|---|---:|");
        builder.AppendLine($"| 均值 | {Format(analysis.Mean)}{unitSuffix} |");
        builder.AppendLine($"| 中位数 | {Format(analysis.Median)}{unitSuffix} |");
        builder.AppendLine($"| 范围 | {Format(analysis.Minimum)} – {Format(analysis.Maximum)}{unitSuffix} |");
        builder.AppendLine($"| 过程内 σ（MR̄ / 1.128） | {Format(analysis.WithinSigma)}{unitSuffix} |");
        builder.AppendLine($"| 整体 σ（样本标准差） | {Format(analysis.OverallSigma)}{unitSuffix} |");
        builder.AppendLine($"| 控制限 | {Format(analysis.LowerControlLimit)} – {Format(analysis.UpperControlLimit)}{unitSuffix} |");
        builder.AppendLine($"| Cp / Cpk | {FormatNullable(analysis.Cp)} / {FormatNullable(analysis.Cpk)} |");
        builder.AppendLine($"| Pp / Ppk | {FormatNullable(analysis.Pp)} / {FormatNullable(analysis.Ppk)} |");
        builder.AppendLine();
        builder.AppendLine("## 规则信号");
        builder.AppendLine();

        if (analysis.Alerts.Count == 0)
        {
            builder.AppendLine("当前数据范围内没有触发已启用的 8 条透明规则。");
        }
        else
        {
            builder.AppendLine("| 时间 | 规则 | 严重度 | 数值 | 证据 |");
            builder.AppendLine("|---|---|---|---:|---|");
            foreach (var alert in analysis.Alerts.Take(50))
            {
                builder.AppendLine(
                    $"| {alert.Timestamp:yyyy-MM-dd HH:mm} | {alert.RuleCode} {Escape(alert.RuleName)} | {Severity(alert.Severity)} | {Format(alert.Value)} | {Escape(alert.Evidence)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 行动闭环");
        builder.AppendLine();
        if (workspace.Actions.Count == 0)
        {
            builder.AppendLine("尚未建立行动项。");
        }
        else
        {
            builder.AppendLine("| 行动 | 负责人 | 状态 | 更新时间 |");
            builder.AppendLine("|---|---|---|---|");
            foreach (var action in workspace.Actions.OrderByDescending(action => action.UpdatedUtc))
            {
                builder.AppendLine(
                    $"| {Escape(action.Title)} | {Escape(action.Owner)} | {ActionStatusText(action.Status)} | {action.UpdatedUtc.ToLocalTime():yyyy-MM-dd HH:mm} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 解释边界");
        builder.AppendLine();
        builder.AppendLine("- 控制限来自当前数据，不等于客户规格限，也不自动证明过程受控。");
        builder.AppendLine("- 规则信号只指出值得调查的统计证据，不推断根因，不替代量具、抽样和现场审核。");
        builder.AppendLine("- Cp/Cpk 使用移动极差估计的过程内 σ；Pp/Ppk 使用整体样本标准差。");
        builder.AppendLine("- 数据少于 20 点时仅适合演示和初筛；正式使用前应由质量专业人员确认规则集与抽样方案。");
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("作者：KBT096 · 许可证：MIT");
        return builder.ToString();
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string FormatNullable(double? value) => value is null ? "—" : Format(value.Value);
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string Severity(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "严重",
        AlertSeverity.Warning => "警告",
        _ => "提示"
    };

    public static string ActionStatusText(ActionStatus status) => status switch
    {
        ActionStatus.Pending => "待确认",
        ActionStatus.Acknowledged => "已确认",
        ActionStatus.Investigating => "调查中",
        ActionStatus.Actioned => "已采取措施",
        ActionStatus.Verified => "待关闭",
        ActionStatus.Closed => "已关闭",
        _ => status.ToString()
    };
}
