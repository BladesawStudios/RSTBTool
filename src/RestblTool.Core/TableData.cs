using System.Globalization;
using System.Text.Json;
using RSTBSharp;
using ZsDicSharp;

namespace RestblTool.Core;

public static class TableData
{
    private const string Extension = ".json.zs";

    public static string PathFor(string dataDir, int version) => Path.Combine(dataDir, version + Extension);

    public static int[] Versions(string dataDir)
        => Directory.Exists(dataDir)
            ? Directory.GetFiles(dataDir, "*" + Extension)
                .Select(f => int.TryParse(Path.GetFileName(f)[..^Extension.Length], out int v) ? v : -1)
                .Where(v => v >= 0)
                .OrderDescending()
                .ToArray()
            : [];

    public static ResourceSizeTable Load(Game game, int version)
    {
        using ZsDic zs = ZsDic.Dictionaryless();
        using JsonDocument json = JsonDocument.Parse(zs.Decompress(File.ReadAllBytes(PathFor(game.DataDir, version)), ZsDictionary.None));

        ResourceSizeTable table = game.NewTable();
        foreach (JsonProperty entry in json.RootElement.GetProperty("Hash Table").EnumerateObject())
            table.Hashes[uint.Parse(entry.Name, CultureInfo.InvariantCulture)] = entry.Value.GetUInt32();
        foreach (JsonProperty entry in json.RootElement.GetProperty("Collision Table").EnumerateObject())
            table.Names[entry.Name] = entry.Value.GetUInt32();
        return table;
    }

    public static void Save(string dataDir, int version, ResourceSizeTable table)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream))
        {
            json.WriteStartObject();
            json.WriteStartObject("Hash Table");
            foreach ((uint hash, uint size) in table.Hashes) json.WriteNumber(hash.ToString(CultureInfo.InvariantCulture), size);
            json.WriteEndObject();
            json.WriteStartObject("Collision Table");
            foreach ((string name, uint size) in table.Names) json.WriteNumber(name, size);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        using ZsDic zs = ZsDic.Dictionaryless();
        File.WriteAllBytes(PathFor(dataDir, version), zs.Compress(stream.ToArray(), ZsDictionary.None, 19));
    }
}
