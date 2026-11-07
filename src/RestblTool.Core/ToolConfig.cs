using System.Text.Json;

namespace RestblTool.Core;

public sealed class ToolConfig
{
    public string? Game { get; set; }
    public string? Mod { get; set; }
    public string? ModsFolder { get; set; }
    public int? Version { get; set; }

    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RestblTool", "config.json");

    public static ToolConfig Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<ToolConfig>(File.ReadAllText(FilePath)) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
