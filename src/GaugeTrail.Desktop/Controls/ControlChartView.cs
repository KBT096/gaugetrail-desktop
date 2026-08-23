using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GaugeTrail.Core;

namespace GaugeTrail.Desktop.Controls;

public sealed class ControlChartView : FrameworkElement
{
    private QualityWorkspace? _workspace;
    private AnalysisResult _analysis = AnalysisResult.Empty;

    public void SetData(QualityWorkspace workspace, AnalysisResult analysis)
    {
        _workspace = workspace;
        _analysis = analysis;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(14, 22, 41)),
            null,
            new Rect(0, 0, ActualWidth, ActualHeight),
            10,
            10);

        if (_workspace is null || _analysis.Count < 2 || ActualWidth < 180 || ActualHeight < 140)
        {
            DrawCenteredText(drawingContext, "等待至少 2 个测量点", new SolidColorBrush(Color.FromRgb(147, 164, 195)));
            return;
        }

        var points = _analysis.OrderedMeasurements.TakeLast(120).ToArray();
        var valuesForRange = points.Select(point => point.Value)
            .Concat([
                _analysis.Mean,
                _analysis.LowerControlLimit,
                _analysis.UpperControlLimit
            ])
            .ToList();

        if (_workspace.LowerSpecLimit is { } lsl)
        {
            valuesForRange.Add(lsl);
        }

        if (_workspace.UpperSpecLimit is { } usl)
        {
            valuesForRange.Add(usl);
        }

        var minimum = valuesForRange.Min();
        var maximum = valuesForRange.Max();
        var range = Math.Max(maximum - minimum, 0.01);
        minimum -= range * 0.12;
        maximum += range * 0.12;

        const double left = 58;
        const double top = 18;
        const double right = 20;
        const double bottom = 40;
        var plot = new Rect(left, top, Math.Max(1, ActualWidth - left - right), Math.Max(1, ActualHeight - top - bottom));
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(36, 50, 77)), 1);
        var labelBrush = new SolidColorBrush(Color.FromRgb(147, 164, 195));

        for (var i = 0; i <= 4; i++)
        {
            var fraction = i / 4d;
            var y = plot.Top + fraction * plot.Height;
            drawingContext.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var tickValue = maximum - fraction * (maximum - minimum);
            DrawText(drawingContext, tickValue.ToString("0.###", CultureInfo.InvariantCulture), 11, labelBrush,
                new Point(6, y - 8));
        }

        double Y(double value) => plot.Bottom - (value - minimum) / (maximum - minimum) * plot.Height;
        double X(int index) => plot.Left + index / Math.Max(1d, points.Length - 1d) * plot.Width;

        DrawReferenceLine(drawingContext, plot, Y(_analysis.Mean), Color.FromRgb(98, 214, 199), "CL", false);
        DrawReferenceLine(drawingContext, plot, Y(_analysis.UpperControlLimit), Color.FromRgb(244, 191, 98), "UCL", true);
        DrawReferenceLine(drawingContext, plot, Y(_analysis.LowerControlLimit), Color.FromRgb(244, 191, 98), "LCL", true);

        if (_workspace.UpperSpecLimit is { } upperSpec)
        {
            DrawReferenceLine(drawingContext, plot, Y(upperSpec), Color.FromRgb(139, 168, 255), "USL", true);
        }

        if (_workspace.LowerSpecLimit is { } lowerSpec)
        {
            DrawReferenceLine(drawingContext, plot, Y(lowerSpec), Color.FromRgb(139, 168, 255), "LSL", true);
        }

        var lineGeometry = new StreamGeometry();
        using (var context = lineGeometry.Open())
        {
            context.BeginFigure(new Point(X(0), Y(points[0].Value)), false, false);
            for (var i = 1; i < points.Length; i++)
            {
                context.LineTo(new Point(X(i), Y(points[i].Value)), true, false);
            }
        }

        lineGeometry.Freeze();
        drawingContext.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(111, 153, 255)), 2), lineGeometry);

        var alertsByMeasurement = _analysis.Alerts
            .GroupBy(alert => alert.MeasurementId)
            .ToDictionary(group => group.Key, group => group.Max(alert => alert.Severity), StringComparer.Ordinal);

        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            var brush = alertsByMeasurement.TryGetValue(point.Id, out var severity)
                ? severity switch
                {
                    AlertSeverity.Critical => new SolidColorBrush(Color.FromRgb(255, 115, 136)),
                    AlertSeverity.Warning => new SolidColorBrush(Color.FromRgb(244, 191, 98)),
                    _ => new SolidColorBrush(Color.FromRgb(180, 139, 255))
                }
                : new SolidColorBrush(Color.FromRgb(98, 214, 199));
            var radius = alertsByMeasurement.ContainsKey(point.Id) ? 5d : 3.2d;
            drawingContext.DrawEllipse(brush, new Pen(new SolidColorBrush(Color.FromRgb(11, 16, 32)), 1.5),
                new Point(X(i), Y(point.Value)), radius, radius);
        }

        DrawText(drawingContext, points[0].Timestamp.ToString("MM-dd HH:mm"), 11, labelBrush,
            new Point(plot.Left, plot.Bottom + 11));
        var lastLabel = points[^1].Timestamp.ToString("MM-dd HH:mm");
        var lastText = CreateText(lastLabel, 11, labelBrush);
        drawingContext.DrawText(lastText, new Point(plot.Right - lastText.Width, plot.Bottom + 11));
    }

    private static void DrawReferenceLine(
        DrawingContext context,
        Rect plot,
        double y,
        Color color,
        string label,
        bool dashed)
    {
        if (y < plot.Top - 1 || y > plot.Bottom + 1)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(color), 1);
        if (dashed)
        {
            pen.DashStyle = new DashStyle([5, 4], 0);
        }

        context.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        DrawText(context, label, 10, new SolidColorBrush(color), new Point(plot.Right - 28, y - 14));
    }

    private void DrawCenteredText(DrawingContext context, string text, Brush brush)
    {
        var formatted = CreateText(text, 14, brush);
        context.DrawText(formatted, new Point((ActualWidth - formatted.Width) / 2, (ActualHeight - formatted.Height) / 2));
    }

    private static void DrawText(DrawingContext context, string text, double size, Brush brush, Point origin)
    {
        context.DrawText(CreateText(text, size, brush), origin);
    }

    private static FormattedText CreateText(string text, double size, Brush brush)
    {
        return new FormattedText(
            text,
            CultureInfo.GetCultureInfo("zh-CN"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            1.0);
    }
}
