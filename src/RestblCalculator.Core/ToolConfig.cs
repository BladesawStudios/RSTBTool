using System.Text.Json;

namespace RestblCalculator.Core;

public sealed class ToolConfig
{
    public string? Game { get; set; }
    public string? Mod { get; set; }
    public string? ModsFolder { get; set; }
    public int? Version { get; set; }
    public float? UiScale { get; set; }

    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "RestblCalculator", "config.json");

    public static ToolConfig Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<ToolConfig>(File.ReadAllText(FilePath)) ?? new() : new();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
