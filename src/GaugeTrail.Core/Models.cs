using System.Text.Json.Serialization;

namespace GaugeTrail.Core;

public sealed class QualityWorkspace
{
    public int SchemaVersion { get; set; } = 1;
    public string WorkspaceId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "未命名工作区";
    public string MetricName { get; set; } = "测量值";
    public string Unit { get; set; } = string.Empty;
    public double? LowerSpecLimit { get; set; }
    public double? UpperSpecLimit { get; set; }
    public double? Target { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<MeasurementRecord> Measurements { get; set; } = [];
    public List<ActionRecord> Actions { get; set; } = [];
    public List<AuditRecord> AuditTrail { get; set; } = [];
}

public sealed class MeasurementRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Batch { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionStatus
{
    Pending,
    Acknowledged,
    Investigating,
    Actioned,
    Verified,
    Closed
}

public sealed class ActionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AlertKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Owner { get; set; } = "未分配";
    public ActionStatus Status { get; set; } = ActionStatus.Pending;
    public string VerificationNote { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuditRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public enum AlertSeverity
{
    Notice,
    Warning,
    Critical
}

public sealed class RuleAlert
{
    public string Key { get; init; } = string.Empty;
    public string RuleCode { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public AlertSeverity Severity { get; init; }
    public int PointIndex { get; init; }
    public string MeasurementId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
}

public sealed class AnalysisResult
{
    public static AnalysisResult Empty { get; } = new();

    public int Count { get; init; }
    public double Mean { get; init; }
    public double Median { get; init; }
    public double Minimum { get; init; }
    public double Maximum { get; init; }
    public double WithinSigma { get; init; }
    public double OverallSigma { get; init; }
    public double LowerControlLimit { get; init; }
    public double UpperControlLimit { get; init; }
    public double? Cp { get; init; }
    public double? Cpk { get; init; }
    public double? Pp { get; init; }
    public double? Ppk { get; init; }
    public string State { get; init; } = "等待数据";
    public string Interpretation { get; init; } = "导入或加载测量数据后开始分析。";
    public IReadOnlyList<MeasurementRecord> OrderedMeasurements { get; init; } = [];
    public IReadOnlyList<RuleAlert> Alerts { get; init; } = [];
}

public sealed class CsvImportResult
{
    public List<MeasurementRecord> Records { get; } = [];
    public List<string> Errors { get; } = [];
    public int SkippedRows { get; set; }
}
