using System.Globalization;
using System.Text;

namespace GaugeTrail.Core;

public static class CsvMeasurementService
{
    private static readonly string[] TimestampHeaders = ["timestamp", "time", "datetime", "时间", "时间戳"];
    private static readonly string[] ValueHeaders = ["value", "measurement", "测量值", "数值"];
    private static readonly string[] BatchHeaders = ["batch", "lot", "批次", "批号"];
    private static readonly string[] SourceHeaders = ["source", "device", "来源", "设备"];
    private static readonly string[] NoteHeaders = ["note", "remark", "备注", "说明"];

    public static CsvImportResult Import(string csvText)
    {
        ArgumentNullException.ThrowIfNull(csvText);

        var result = new CsvImportResult();
        var logicalRows = ReadLogicalRows(csvText).Where(row => !string.IsNullOrWhiteSpace(row)).ToArray();
        if (logicalRows.Length == 0)
        {
            result.Errors.Add("CSV 文件为空。");
            return result;
        }

        var delimiter = DetectDelimiter(logicalRows[0]);
        var headers = ParseRow(logicalRows[0], delimiter)
            .Select(header => header.Trim().TrimStart('\uFEFF'))
            .ToArray();
        var timestampIndex = FindHeader(headers, TimestampHeaders);
        var valueIndex = FindHeader(headers, ValueHeaders);
        var batchIndex = FindHeader(headers, BatchHeaders);
        var sourceIndex = FindHeader(headers, SourceHeaders);
        var noteIndex = FindHeader(headers, NoteHeaders);

        if (timestampIndex < 0 || valueIndex < 0)
        {
            result.Errors.Add("CSV 必须包含 timestamp（时间）和 value（测量值）两列。");
            return result;
        }

        for (var rowNumber = 2; rowNumber <= logicalRows.Length; rowNumber++)
        {
            var cells = ParseRow(logicalRows[rowNumber - 1], delimiter);
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (!TryCell(cells, timestampIndex, out var timestampText)
                || !TryParseTimestamp(timestampText, out var timestamp))
            {
                AddRowError(result, rowNumber, "时间无法识别");
                continue;
            }

            if (!TryCell(cells, valueIndex, out var valueText)
                || !TryParseDouble(valueText, out var value)
                || !double.IsFinite(value))
            {
                AddRowError(result, rowNumber, "测量值不是有限数字");
                continue;
            }

            result.Records.Add(new MeasurementRecord
            {
                Timestamp = timestamp,
                Value = value,
                Batch = ReadOptional(cells, batchIndex),
                Source = ReadOptional(cells, sourceIndex),
                Note = ReadOptional(cells, noteIndex)
            });
        }

        return result;
    }

    public static string Export(IEnumerable<MeasurementRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var builder = new StringBuilder();
        builder.AppendLine("timestamp,value,batch,source,note");
        foreach (var record in records.OrderBy(record => record.Timestamp))
        {
            builder
                .Append(Escape(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))
                .Append(',')
                .Append(record.Value.ToString("G17", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(Escape(record.Batch))
                .Append(',')
                .Append(Escape(record.Source))
                .Append(',')
                .Append(Escape(record.Note))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static IEnumerable<string> ReadLogicalRows(string text)
    {
        using var reader = new StringReader(text);
        var builder = new StringBuilder();
        var quoted = false;

        while (reader.ReadLine() is { } line)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] != '"')
                {
                    continue;
                }

                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                quoted = !quoted;
            }

            if (!quoted)
            {
                yield return builder.ToString();
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static char DetectDelimiter(string header)
    {
        var candidates = new[] { ',', '\t', ';' };
        return candidates
            .OrderByDescending(candidate => ParseRow(header, candidate).Count)
            .First();
    }

    private static List<string> ParseRow(string row, char delimiter)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < row.Length; i++)
        {
            var character = row[i];
            if (character == '"')
            {
                if (quoted && i + 1 < row.Length && row[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == delimiter && !quoted)
            {
                values.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(character);
            }
        }

        values.Add(builder.ToString());
        return values;
    }

    private static int FindHeader(IReadOnlyList<string> headers, IReadOnlyList<string> aliases)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (aliases.Any(alias => alias.Equals(headers[i], StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryCell(IReadOnlyList<string> cells, int index, out string value)
    {
        if (index >= 0 && index < cells.Count)
        {
            value = cells[index].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string ReadOptional(IReadOnlyList<string> cells, int index)
    {
        return TryCell(cells, index, out var value) ? value : string.Empty;
    }

    private static bool TryParseTimestamp(string text, out DateTime timestamp)
    {
        var cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("zh-CN"),
            CultureInfo.CurrentCulture
        };

        foreach (var culture in cultures)
        {
            if (DateTime.TryParse(text, culture, DateTimeStyles.AssumeLocal, out timestamp))
            {
                return true;
            }
        }

        timestamp = default;
        return false;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static string Escape(string? value)
    {
        var normalized = value ?? string.Empty;
        if (normalized.Contains('"'))
        {
            normalized = normalized.Replace("\"", "\"\"", StringComparison.Ordinal);
        }

        return normalized.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{normalized}\"" : normalized;
    }

    private static void AddRowError(CsvImportResult result, int rowNumber, string reason)
    {
        result.SkippedRows++;
        if (result.Errors.Count < 20)
        {
            result.Errors.Add($"第 {rowNumber} 行：{reason}。");
        }
    }
}
