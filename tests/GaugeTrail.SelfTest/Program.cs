using System.Text;
using GaugeTrail.Core;

var checks = 0;
var temporaryDirectory = Path.Combine(Path.GetTempPath(), "gaugetrail-selftest-" + Guid.NewGuid().ToString("N"));

try
{
    Check(StatisticalAnalyzer.Median([1, 4, 2, 3]) == 2.5, "偶数样本中位数");
    Check(Math.Abs(StatisticalAnalyzer.SampleStandardDeviation([1, 2, 3]) - 1) < 1e-10, "样本标准差");

    AssertRule("R1", [0.0, 0.1, -0.1, 3.2]);
    AssertRule("R2", Enumerable.Repeat(0.5, 9).ToArray());
    AssertRule("R3", [0.1, 0.2, 0.3, 0.4, 0.5, 0.6]);
    AssertRule("R4", [0.2, -0.2, 0.3, -0.3, 0.4, -0.4, 0.5, -0.5, 0.6, -0.6, 0.7, -0.7, 0.8, -0.8]);
    AssertRule("R5", [2.2, 2.3, 0.1]);
    AssertRule("R6", [1.2, 1.3, 1.4, 1.5, 0.1]);
    AssertRule("R7", [0.4, -0.3, 0.2, -0.1, 0.5, -0.4, 0.3, -0.2, 0.1, -0.5, 0.4, -0.3, 0.2, -0.1, 0.5]);
    AssertRule("R8", [1.2, -1.3, 1.4, -1.5, 1.6, -1.7, 1.8, -1.9]);
    Check(CountRule("R2", Enumerable.Repeat(0.5, 12).ToArray()) == 1, "重叠规则窗口合并");
    Check(CountRule("R1", [3.2, 3.3]) == 2, "R1 单点信号不合并");

    var csv = """
              timestamp,value,batch,source,note
              2026-08-21 08:00:00,10.02,B-01,千分尺,"首件, 已复核"
              2026-08-21 08:20:00,10.08,B-01,千分尺,
              bad-time,abc,B-02,千分尺,错误行
              """;
    var import = CsvMeasurementService.Import(csv);
    Check(import.Records.Count == 2, "CSV 有效行导入");
    Check(import.SkippedRows == 1 && import.Errors.Count == 1, "CSV 错误行隔离");
    Check(import.Records[0].Note == "首件, 已复核", "CSV 引号字段");

    var exported = CsvMeasurementService.Export(import.Records);
    var roundTrip = CsvMeasurementService.Import(exported);
    Check(roundTrip.Records.Count == 2, "CSV 导出回读");
    Check(Math.Abs(roundTrip.Records[1].Value - 10.08) < 1e-10, "CSV 数值无损回读");

    var workspace = SampleWorkspaceFactory.Create();
    var analysis = StatisticalAnalyzer.Analyze(workspace);
    Check(analysis.Count == 58, "示例数据点数量");
    Check(analysis.Alerts.Any(alert => alert.RuleCode == "R1"), "示例特殊原因点");
    Check(analysis.Cpk is not null && double.IsFinite(analysis.Cpk.Value), "能力指数计算");
    Check(analysis.UpperControlLimit > analysis.Mean, "控制上限");

    Directory.CreateDirectory(temporaryDirectory);
    var workspacePath = Path.Combine(temporaryDirectory, "workspace.json");
    WorkspaceStore.Save(workspacePath, workspace);
    var loaded = WorkspaceStore.Load(workspacePath);
    Check(loaded.WorkspaceId == workspace.WorkspaceId, "工作区标识回读");
    Check(loaded.Measurements.Count == workspace.Measurements.Count, "工作区测量数据回读");

    var report = MarkdownReportBuilder.Build(workspace, analysis, DateTimeOffset.Now.DateTime);
    Check(report.Contains("# A 线 · 轴套外径 · 过程快照", StringComparison.Ordinal), "Markdown 标题");
    Check(report.Contains("作者：KBT096", StringComparison.Ordinal), "Markdown 作者署名");
    Check(report.Contains("解释边界", StringComparison.Ordinal), "Markdown 解释边界");

    var invalid = SampleWorkspaceFactory.Create();
    invalid.LowerSpecLimit = 11;
    invalid.UpperSpecLimit = 10;
    var invalidPath = Path.Combine(temporaryDirectory, "invalid.json");
    var rejected = false;
    try
    {
        WorkspaceStore.Save(invalidPath, invalid);
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }

    Check(rejected, "无效规格限拒绝");

    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine($"PASS: {checks} checks completed");
    return 0;
}
catch (Exception exception)
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.Error.WriteLine($"FAIL after {checks} checks: {exception.Message}");
    Console.Error.WriteLine(exception);
    return 1;
}
finally
{
    if (Directory.Exists(temporaryDirectory))
    {
        Directory.Delete(temporaryDirectory, true);
    }
}

void AssertRule(string code, IReadOnlyList<double> values)
{
    Check(CountRule(code, values) > 0, $"规则 {code}");
}

int CountRule(string code, IReadOnlyList<double> values)
{
    var start = new DateTime(2026, 8, 21, 8, 0, 0, DateTimeKind.Local);
    var points = values.Select((value, index) => new MeasurementRecord
    {
        Id = $"point-{index}",
        Timestamp = start.AddMinutes(index),
        Value = value
    }).ToArray();
    var alerts = RuleEngine.Evaluate(points, 0, 1);
    return alerts.Count(alert => alert.RuleCode == code);
}

void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"检查失败：{name}");
    }

    checks++;
}
