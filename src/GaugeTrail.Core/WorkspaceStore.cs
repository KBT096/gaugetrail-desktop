using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaugeTrail.Core;

public static class WorkspaceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static QualityWorkspace Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path, Encoding.UTF8);
        var workspace = JsonSerializer.Deserialize<QualityWorkspace>(json, Options)
                        ?? throw new InvalidDataException("工作区 JSON 内容为空。");
        Validate(workspace);
        return workspace;
    }

    public static void Save(string path, QualityWorkspace workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(workspace);
        Validate(workspace);

        workspace.UpdatedUtc = DateTime.UtcNow;
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
                        ?? throw new InvalidOperationException("无法确定工作区目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = fullPath + ".tmp";
        var backupPath = fullPath + ".bak";
        var json = JsonSerializer.Serialize(workspace, Options);
        File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

        if (File.Exists(fullPath))
        {
            File.Replace(temporaryPath, fullPath, backupPath, true);
        }
        else
        {
            File.Move(temporaryPath, fullPath);
        }
    }

    public static string Serialize(QualityWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        Validate(workspace);
        return JsonSerializer.Serialize(workspace, Options);
    }

    private static void Validate(QualityWorkspace workspace)
    {
        if (workspace.SchemaVersion != 1)
        {
            throw new InvalidDataException($"不支持的工作区版本：{workspace.SchemaVersion}。");
        }

        if (string.IsNullOrWhiteSpace(workspace.WorkspaceId))
        {
            throw new InvalidDataException("工作区缺少标识。");
        }

        if (workspace.LowerSpecLimit is not null
            && workspace.UpperSpecLimit is not null
            && workspace.LowerSpecLimit >= workspace.UpperSpecLimit)
        {
            throw new InvalidDataException("规格下限必须小于规格上限。");
        }

        if (workspace.Measurements.Any(record => !double.IsFinite(record.Value)))
        {
            throw new InvalidDataException("工作区包含非有限测量值。");
        }
    }
}
