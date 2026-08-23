namespace GaugeTrail.Core;

public static class SampleWorkspaceFactory
{
    public static QualityWorkspace Create()
    {
        var workspace = new QualityWorkspace
        {
            Name = "A 线 · 轴套外径",
            MetricName = "轴套外径",
            Unit = "mm",
            LowerSpecLimit = 9.40,
            UpperSpecLimit = 10.60,
            Target = 10.00,
            CreatedUtc = DateTime.UtcNow.AddDays(-3),
            UpdatedUtc = DateTime.UtcNow
        };

        var pattern = new[]
        {
            -0.08, -0.02, 0.04, 0.09, 0.03, -0.05, -0.10, -0.03,
            0.05, 0.11, 0.02, -0.06, -0.04, 0.07, 0.10, 0.01
        };
        var start = DateTime.Today.AddHours(8).AddDays(-2);

        for (var i = 0; i < 48; i++)
        {
            workspace.Measurements.Add(new MeasurementRecord
            {
                Timestamp = start.AddMinutes(i * 20),
                Value = Math.Round(10 + pattern[i % pattern.Length] + ((i % 5) - 2) * 0.004, 3),
                Batch = i < 24 ? "B240821-A" : "B240821-B",
                Source = "数字千分尺",
                Note = i % 17 == 0 ? "换班复核" : string.Empty
            });
        }

        var shifted = new[] { 10.20, 10.23, 10.25, 10.27, 10.30, 10.32, 10.34, 10.36, 10.38 };
        for (var i = 0; i < shifted.Length; i++)
        {
            workspace.Measurements.Add(new MeasurementRecord
            {
                Timestamp = start.AddMinutes((48 + i) * 20),
                Value = shifted[i],
                Batch = "B240821-C",
                Source = "数字千分尺",
                Note = "刀补后观察"
            });
        }

        workspace.Measurements.Add(new MeasurementRecord
        {
            Timestamp = start.AddMinutes(57 * 20),
            Value = 10.92,
            Batch = "B240821-C",
            Source = "数字千分尺",
            Note = "演示用特殊原因点"
        });

        workspace.AuditTrail.Add(new AuditRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-12),
            Category = "示例",
            Summary = "创建离线演示工作区",
            Details = "已载入 58 条示例测量记录。"
        });

        return workspace;
    }
}
