using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GaugeTrail.Core;
using Microsoft.Win32;

namespace GaugeTrail.Desktop;

public partial class MainWindow : Window
{
    private readonly string _dataDirectory;
    private readonly string _workspacePath;
    private QualityWorkspace _workspace = new();
    private AnalysisResult _analysis = AnalysisResult.Empty;
    private IReadOnlyList<AlertRow> _alertRows = [];
    private IReadOnlyList<ActionRow> _actionRows = [];
    private bool _loaded;
    private bool _autoSaveEnabled = true;

    public MainWindow()
    {
        InitializeComponent();
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GaugeTrail");
        _workspacePath = Path.Combine(_dataDirectory, "workspace.json");
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(_workspacePath))
            {
                _workspace = WorkspaceStore.Load(_workspacePath);
            }
            else
            {
                _workspace = SampleWorkspaceFactory.Create();
                WorkspaceStore.Save(_workspacePath, _workspace);
            }
        }
        catch (Exception exception)
        {
            _autoSaveEnabled = false;
            _workspace = SampleWorkspaceFactory.Create();
            MessageBox.Show(
                "现有工作区无法读取，因此应用只在内存中载入了演示数据，原文件没有被覆盖。\n\n"
                + exception.Message
                + "\n\n请先备份或修复本地 workspace.json；也可以点击“恢复演示数据”明确创建新工作区。",
                "GaugeTrail Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        _loaded = true;
        RefreshAll();
        DataPathText.Text = _workspacePath;
        QuickBatchTextBox.Text = _workspace.Measurements.LastOrDefault()?.Batch ?? string.Empty;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_loaded || !_autoSaveEnabled)
        {
            return;
        }

        try
        {
            WorkspaceStore.Save(_workspacePath, _workspace);
        }
        catch (Exception exception)
        {
            var choice = MessageBox.Show(
                $"关闭前保存失败：\n\n{exception.Message}\n\n仍然关闭吗？",
                "GaugeTrail Desktop",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            e.Cancel = choice != MessageBoxResult.Yes;
        }
    }

    private void RefreshAll()
    {
        _analysis = StatisticalAnalyzer.Analyze(_workspace);
        ControlChart.SetData(_workspace, _analysis);

        StatCountText.Text = _analysis.Count.ToString(CultureInfo.InvariantCulture);
        StatMeanText.Text = _analysis.Count == 0 ? "—" : Format(_analysis.Mean);
        StatUnitText.Text = string.IsNullOrWhiteSpace(_workspace.Unit)
            ? _workspace.MetricName
            : $"{_workspace.MetricName} · {_workspace.Unit}";
        StatCpkText.Text = FormatNullable(_analysis.Cpk);
        StatAlertText.Text = _analysis.Alerts.Count.ToString(CultureInfo.InvariantCulture);
        StateText.Text = _analysis.State;
        StateText.Foreground = StateBrush(_analysis.State);
        InterpretationText.Text = _analysis.Interpretation;
        SpecText.Text = BuildSpecText();
        HeaderUpdatedText.Text = $"本地保存 · {_workspace.UpdatedUtc.ToLocalTime():HH:mm}";

        MeasurementsGrid.ItemsSource = _workspace.Measurements
            .OrderByDescending(record => record.Timestamp)
            .ToArray();

        _alertRows = _analysis.Alerts
            .Select(alert => new AlertRow(
                alert,
                SeverityText(alert.Severity),
                $"{alert.RuleCode} · {alert.RuleName}",
                alert.Timestamp.ToString("MM-dd HH:mm"),
                Format(alert.Value),
                alert.Evidence))
            .ToArray();
        AlertsGrid.ItemsSource = _alertRows;
        AlertSummaryText.Text = $"{_alertRows.Count} 条信号 · {_alertRows.Count(row => row.Alert.Severity == AlertSeverity.Critical)} 条严重";

        RecentAlertsList.ItemsSource = _analysis.Alerts
            .Take(3)
            .Select(alert => new DashboardAlertRow(
                $"{alert.RuleCode} · {alert.RuleName}",
                $"{alert.Timestamp:MM-dd HH:mm} · {Format(alert.Value)} {_workspace.Unit}".Trim(),
                alert.Evidence,
                SeverityBrush(alert.Severity)))
            .ToArray();

        if (_analysis.Alerts.Count == 0)
        {
            RecentAlertsList.ItemsSource = new[]
            {
                new DashboardAlertRow(
                    "当前没有规则信号",
                    "继续按既定抽样方案记录",
                    "没有信号不等于没有风险，仍需结合现场与量具信息。",
                    (Brush)FindResource("PrimaryBrush"))
            };
        }

        _actionRows = _workspace.Actions
            .OrderByDescending(action => action.UpdatedUtc)
            .Select(action => new ActionRow(
                action,
                action.Title,
                action.Owner,
                MarkdownReportBuilder.ActionStatusText(action.Status),
                action.UpdatedUtc.ToLocalTime().ToString("MM-dd HH:mm")))
            .ToArray();
        ActionsGrid.ItemsSource = _actionRows;
        DashboardActionCountText.Text =
            $"{_workspace.Actions.Count(action => action.Status != ActionStatus.Closed)} 个未关闭行动项";

        AuditList.ItemsSource = _workspace.AuditTrail
            .OrderByDescending(record => record.TimestampUtc)
            .Take(8)
            .Select(record => new AuditRow(
                record.Summary,
                $"{record.TimestampUtc.ToLocalTime():MM-dd HH:mm} · {record.Details}"))
            .ToArray();

        WorkspaceNameTextBox.Text = _workspace.Name;
        MetricNameTextBox.Text = _workspace.MetricName;
        UnitTextBox.Text = _workspace.Unit;
        LowerSpecTextBox.Text = FormatEditable(_workspace.LowerSpecLimit);
        TargetTextBox.Text = FormatEditable(_workspace.Target);
        UpperSpecTextBox.Text = FormatEditable(_workspace.UpperSpecLimit);

        if (AlertsGrid.SelectedItem is null)
        {
            ResetAlertDetail();
        }

        if (ActionsGrid.SelectedItem is null)
        {
            ResetActionDetail();
        }
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button source || source.CommandParameter is not string destination)
        {
            return;
        }

        var pages = new Dictionary<string, (FrameworkElement Element, string Title, string Subtitle)>
        {
            ["Dashboard"] = (DashboardPage, "过程总览", "先看证据，再决定下一步。"),
            ["Data"] = (DataPage, "测量数据", "把每一条数值连回时间、批次和来源。"),
            ["Alerts"] = (AlertsPage, "规则信号", "每条信号都有规则、窗口与证据。"),
            ["Actions"] = (ActionsPage, "行动闭环", "从发现到验证，状态变化全部留痕。"),
            ["Reports"] = (ReportsPage, "报告与导出", "把分析带走，不把数据上传。"),
            ["Settings"] = (SettingsPage, "工作区设置", "规格属于业务，控制限来自数据。")
        };

        if (!pages.TryGetValue(destination, out var target))
        {
            return;
        }

        foreach (var page in pages.Values.Select(value => value.Element))
        {
            page.Visibility = Visibility.Collapsed;
        }

        target.Element.Visibility = Visibility.Visible;
        PageTitleText.Text = target.Title;
        PageSubtitleText.Text = target.Subtitle;

        foreach (var button in FindVisualChildren<Button>(this)
                     .Where(button => button.CommandParameter is string parameter && pages.ContainsKey(parameter)))
        {
            button.Tag = button.CommandParameter as string == destination ? "Selected" : null;
        }
    }

    private void QuickAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDouble(QuickValueTextBox.Text, out var value) || !double.IsFinite(value))
        {
            MessageBox.Show("请输入有效的有限数字。", "记录测量值", MessageBoxButton.OK, MessageBoxImage.Information);
            QuickValueTextBox.Focus();
            return;
        }

        _workspace.Measurements.Add(new MeasurementRecord
        {
            Timestamp = DateTime.Now,
            Value = value,
            Batch = QuickBatchTextBox.Text.Trim(),
            Source = "手工录入",
            Note = QuickNoteTextBox.Text.Trim()
        });
        AddAudit("数据", "手工记录测量值", $"{Format(value)} {_workspace.Unit}".Trim());
        SaveAndRefresh();
        QuickValueTextBox.Clear();
        QuickNoteTextBox.Clear();
        QuickValueTextBox.Focus();
    }

    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入测量数据",
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            ImportCsv(dialog.FileName);
        }
    }

    private void ImportCsv(string path)
    {
        CsvImportResult result;
        try
        {
            result = CsvMeasurementService.Import(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception exception)
        {
            MessageBox.Show($"CSV 读取失败：\n\n{exception.Message}", "导入 CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (result.Records.Count == 0)
        {
            MessageBox.Show(
                "没有可导入的有效记录。\n\n" + string.Join("\n", result.Errors.Take(8)),
                "导入 CSV",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var choice = MessageBox.Show(
            $"识别到 {result.Records.Count} 条有效记录，跳过 {result.SkippedRows} 条错误记录。\n\n"
            + "选择“是”替换当前测量数据；选择“否”与当前数据合并；选择“取消”不做更改。",
            "导入 CSV",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (choice == MessageBoxResult.Cancel)
        {
            return;
        }

        var before = _workspace.Measurements.Count;
        if (choice == MessageBoxResult.Yes)
        {
            _workspace.Measurements = result.Records;
        }
        else
        {
            var existing = _workspace.Measurements
                .Select(record => MeasurementSignature(record))
                .ToHashSet(StringComparer.Ordinal);
            _workspace.Measurements.AddRange(result.Records.Where(record => existing.Add(MeasurementSignature(record))));
        }

        AddAudit(
            "导入",
            choice == MessageBoxResult.Yes ? "替换导入 CSV" : "合并导入 CSV",
            $"{Path.GetFileName(path)}：{before} → {_workspace.Measurements.Count} 条；跳过 {result.SkippedRows} 条。");
        _autoSaveEnabled = true;
        SaveAndRefresh();

        if (result.Errors.Count > 0)
        {
            MessageBox.Show(
                $"导入完成，以下内容被跳过：\n\n{string.Join("\n", result.Errors.Take(8))}",
                "导入 CSV",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void LoadSample_Click(object sender, RoutedEventArgs e)
    {
        var choice = MessageBox.Show(
            "这会用内置演示工作区替换当前测量、行动和审计记录。\n\n继续吗？",
            "恢复演示数据",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (choice != MessageBoxResult.Yes)
        {
            return;
        }

        _workspace = SampleWorkspaceFactory.Create();
        _workspace.AuditTrail.Add(new AuditRecord
        {
            Category = "工作区",
            Summary = "恢复内置演示数据",
            Details = "用户明确确认替换当前工作区。"
        });
        _autoSaveEnabled = true;
        SaveAndRefresh();
    }

    private void AlertsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AlertsGrid.SelectedItem is not AlertRow row)
        {
            ResetAlertDetail();
            return;
        }

        SelectedAlertTitleText.Text = $"{row.Alert.RuleCode} · {row.Alert.RuleName}";
        SelectedAlertMessageText.Text = row.Alert.Message;
        SelectedAlertEvidenceText.Text = $"窗口证据：{row.Alert.Evidence} 触发点：{row.Alert.Timestamp:yyyy-MM-dd HH:mm:ss}，值 {Format(row.Alert.Value)} {_workspace.Unit}".Trim();
        CreateActionButton.IsEnabled = true;
    }

    private void CreateAction_Click(object sender, RoutedEventArgs e)
    {
        if (AlertsGrid.SelectedItem is not AlertRow row)
        {
            return;
        }

        var existing = _workspace.Actions.FirstOrDefault(action =>
            action.AlertKey.Equals(row.Alert.Key, StringComparison.Ordinal)
            && action.Status != ActionStatus.Closed);
        if (existing is not null)
        {
            MessageBox.Show("这条信号已经有一个未关闭行动项。", "行动闭环", MessageBoxButton.OK, MessageBoxImage.Information);
            NavigateTo("Actions");
            SelectAction(existing.Id);
            return;
        }

        var action = new ActionRecord
        {
            AlertKey = row.Alert.Key,
            Title = $"调查 {row.Alert.RuleCode} · {row.Alert.RuleName}",
            Owner = "未分配",
            Status = ActionStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        _workspace.Actions.Add(action);
        AddAudit("行动", "创建行动项", action.Title);
        SaveAndRefresh();
        NavigateTo("Actions");
        SelectAction(action.Id);
    }

    private void ActionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActionsGrid.SelectedItem is not ActionRow row)
        {
            ResetActionDetail();
            return;
        }

        ActionOwnerTextBox.IsEnabled = true;
        ActionVerificationTextBox.IsEnabled = true;
        SaveActionButton.IsEnabled = true;
        AdvanceActionButton.IsEnabled = row.Action.Status != ActionStatus.Closed;
        ActionOwnerTextBox.Text = row.Action.Owner;
        ActionVerificationTextBox.Text = row.Action.VerificationNote;
    }

    private void SaveAction_Click(object sender, RoutedEventArgs e)
    {
        if (ActionsGrid.SelectedItem is not ActionRow row)
        {
            return;
        }

        row.Action.Owner = string.IsNullOrWhiteSpace(ActionOwnerTextBox.Text)
            ? "未分配"
            : ActionOwnerTextBox.Text.Trim();
        row.Action.VerificationNote = ActionVerificationTextBox.Text.Trim();
        row.Action.UpdatedUtc = DateTime.UtcNow;
        AddAudit("行动", "更新行动说明", row.Action.Title);
        var id = row.Action.Id;
        SaveAndRefresh();
        SelectAction(id);
    }

    private void AdvanceAction_Click(object sender, RoutedEventArgs e)
    {
        if (ActionsGrid.SelectedItem is not ActionRow row)
        {
            return;
        }

        row.Action.Owner = string.IsNullOrWhiteSpace(ActionOwnerTextBox.Text)
            ? "未分配"
            : ActionOwnerTextBox.Text.Trim();
        row.Action.VerificationNote = ActionVerificationTextBox.Text.Trim();

        if (row.Action.Status == ActionStatus.Actioned
            && string.IsNullOrWhiteSpace(row.Action.VerificationNote))
        {
            MessageBox.Show(
                "从“已采取措施”推进到“待关闭”前，请填写验证备注。",
                "行动闭环",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ActionVerificationTextBox.Focus();
            return;
        }

        if (row.Action.Status == ActionStatus.Closed)
        {
            return;
        }

        var previous = row.Action.Status;
        row.Action.Status++;
        row.Action.UpdatedUtc = DateTime.UtcNow;
        AddAudit(
            "行动",
            "推进行动状态",
            $"{row.Action.Title}：{MarkdownReportBuilder.ActionStatusText(previous)} → {MarkdownReportBuilder.ActionStatusText(row.Action.Status)}");
        var id = row.Action.Id;
        SaveAndRefresh();
        SelectAction(id);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseNullableDouble(LowerSpecTextBox.Text, out var lower)
            || !TryParseNullableDouble(TargetTextBox.Text, out var target)
            || !TryParseNullableDouble(UpperSpecTextBox.Text, out var upper))
        {
            MessageBox.Show("规格限和目标值应为数字，也可以留空。", "工作区设置", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (lower is not null && upper is not null && lower >= upper)
        {
            MessageBox.Show("规格下限必须小于规格上限。", "工作区设置", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _workspace.Name = string.IsNullOrWhiteSpace(WorkspaceNameTextBox.Text)
            ? "未命名工作区"
            : WorkspaceNameTextBox.Text.Trim();
        _workspace.MetricName = string.IsNullOrWhiteSpace(MetricNameTextBox.Text)
            ? "测量值"
            : MetricNameTextBox.Text.Trim();
        _workspace.Unit = UnitTextBox.Text.Trim();
        _workspace.LowerSpecLimit = lower;
        _workspace.Target = target;
        _workspace.UpperSpecLimit = upper;
        AddAudit("设置", "更新工作区资料", $"{_workspace.Name} · {_workspace.MetricName}");
        SaveAndRefresh();
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        ExportText(
            "导出测量数据",
            "CSV 文件 (*.csv)|*.csv",
            "gaugetrail-measurements.csv",
            CsvMeasurementService.Export(_workspace.Measurements));
    }

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        ExportText(
            "导出过程快照",
            "Markdown 文件 (*.md)|*.md",
            "gaugetrail-process-report.md",
            MarkdownReportBuilder.Build(_workspace, _analysis, DateTime.Now));
    }

    private void ExportWorkspace_Click(object sender, RoutedEventArgs e)
    {
        ExportText(
            "导出完整工作区",
            "JSON 文件 (*.json)|*.json",
            "gaugetrail-workspace.json",
            WorkspaceStore.Serialize(_workspace));
    }

    private void ExportText(string title, string filter, string fileName, string contents)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = fileName,
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, contents, new UTF8Encoding(false));
            HeaderUpdatedText.Text = $"已导出 · {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show($"导出失败：\n\n{exception.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_dataDirectory}\"",
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show($"无法打开数据目录：\n\n{exception.Message}", "本地数据", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                    && e.Data.GetData(DataFormats.FileDrop) is string[] files
                    && files.Length == 1
                    && Path.GetExtension(files[0]).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files
            && Path.GetExtension(files[0]).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ImportCsv(files[0]);
        }
    }

    private void SaveAndRefresh()
    {
        try
        {
            WorkspaceStore.Save(_workspacePath, _workspace);
            _autoSaveEnabled = true;
            RefreshAll();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"本地保存失败，当前窗口中的更改仍保留：\n\n{exception.Message}",
                "GaugeTrail Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void AddAudit(string category, string summary, string details)
    {
        _workspace.AuditTrail.Add(new AuditRecord
        {
            TimestampUtc = DateTime.UtcNow,
            Category = category,
            Summary = summary,
            Details = details
        });
    }

    private void NavigateTo(string destination)
    {
        var button = FindVisualChildren<Button>(this)
            .FirstOrDefault(candidate => destination.Equals(candidate.CommandParameter as string, StringComparison.Ordinal));
        if (button is not null)
        {
            Navigate_Click(button, new RoutedEventArgs());
        }
    }

    private void SelectAction(string actionId)
    {
        var row = _actionRows.FirstOrDefault(candidate => candidate.Action.Id.Equals(actionId, StringComparison.Ordinal));
        if (row is not null)
        {
            ActionsGrid.SelectedItem = row;
            ActionsGrid.ScrollIntoView(row);
        }
    }

    private void ResetAlertDetail()
    {
        SelectedAlertTitleText.Text = "选择一条信号查看解释";
        SelectedAlertMessageText.Text = "规则只负责指出窗口证据，真正的原因需要结合现场调查。";
        SelectedAlertEvidenceText.Text = string.Empty;
        CreateActionButton.IsEnabled = false;
    }

    private void ResetActionDetail()
    {
        ActionOwnerTextBox.Text = string.Empty;
        ActionVerificationTextBox.Text = string.Empty;
        ActionOwnerTextBox.IsEnabled = false;
        ActionVerificationTextBox.IsEnabled = false;
        SaveActionButton.IsEnabled = false;
        AdvanceActionButton.IsEnabled = false;
    }

    private string BuildSpecText()
    {
        var lower = _workspace.LowerSpecLimit is null ? "—" : Format(_workspace.LowerSpecLimit.Value);
        var target = _workspace.Target is null ? "—" : Format(_workspace.Target.Value);
        var upper = _workspace.UpperSpecLimit is null ? "—" : Format(_workspace.UpperSpecLimit.Value);
        var unit = string.IsNullOrWhiteSpace(_workspace.Unit) ? string.Empty : $" {_workspace.Unit}";
        return $"LSL {lower} · Target {target} · USL {upper}{unit}";
    }

    private static string MeasurementSignature(MeasurementRecord record)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{record.Timestamp:O}|{record.Value:G17}|{record.Batch}|{record.Source}");
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static bool TryParseNullableDouble(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (TryParseDouble(text, out var parsed) && double.IsFinite(parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string FormatNullable(double? value) => value is null ? "—" : Format(value.Value);
    private static string FormatEditable(double? value) => value is null ? string.Empty : Format(value.Value);

    private static string SeverityText(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "严重",
        AlertSeverity.Warning => "警告",
        _ => "提示"
    };

    private Brush SeverityBrush(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => (Brush)FindResource("DangerBrush"),
        AlertSeverity.Warning => (Brush)FindResource("WarningBrush"),
        _ => new SolidColorBrush(Color.FromRgb(180, 139, 255))
    };

    private Brush StateBrush(string state)
    {
        return state switch
        {
            "暂未发现异常" => (Brush)FindResource("PrimaryBrush"),
            "能力需关注" => (Brush)FindResource("WarningBrush"),
            "存在非随机信号" => (Brush)FindResource("WarningBrush"),
            "需立即检查" => (Brush)FindResource("DangerBrush"),
            "能力不足" => (Brush)FindResource("DangerBrush"),
            _ => (Brush)FindResource("MutedTextBrush")
        };
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dependencyObject)
        where T : DependencyObject
    {
        if (dependencyObject is null)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
        {
            var child = VisualTreeHelper.GetChild(dependencyObject, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record DashboardAlertRow(string Title, string Subtitle, string Evidence, Brush Marker);
    private sealed record AlertRow(
        RuleAlert Alert,
        string Severity,
        string Rule,
        string Time,
        string Value,
        string Evidence);
    private sealed record ActionRow(
        ActionRecord Action,
        string Title,
        string Owner,
        string Status,
        string Updated);
    private sealed record AuditRow(string Summary, string Detail);
}
