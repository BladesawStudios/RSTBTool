using System.Collections.Concurrent;
using RSTBSharp;
using SarcSharp;

namespace RestblTool.Core;

public static class VanillaImporter
{
    public static int Import(Game game, int version, IReadOnlyList<(string Root, string Prefix)> roots, IProgress<(int Done, int Total)>? progress = null)
    {
        Directory.CreateDirectory(game.DataDir);
        game.PrepareImport(roots[^1].Root);

        Dictionary<ulong, ulong> existing = Checksums.Load(game.DataDir);
        int[] older = TableData.Versions(game.DataDir);
        if (older.Length > 0 && older[0] >= version)
            throw new InvalidOperationException($"{game.Name} already has {RestblGenerator.VersionName(older[0])}; versions go in oldest first.");

        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach ((string root, string prefix) in roots)
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                files[prefix + Path.GetRelativePath(root, file).Replace('\\', '/')] = file;

        string table = files.FirstOrDefault(f => f.Key.StartsWith("System/Resource/ResourceSizeTable", StringComparison.Ordinal)).Value
            ?? throw new FileNotFoundException("No RESTBL under System/Resource in the dump.");

        ConcurrentDictionary<ulong, ulong> added = new();
        ConcurrentBag<string> failures = [];
        int done = 0;
        Parallel.ForEach(files, file =>
        {
            try
            {
                Add(file.Key, file.Key, File.ReadAllBytes(file.Value), 0);
            }
            catch (Exception e)
            {
                failures.Add($"{file.Key}: {e.Message}");
            }

            progress?.Report((Interlocked.Increment(ref done), files.Count));
        });

        if (!failures.IsEmpty)
            throw new InvalidDataException($"{failures.Count} files couldn't be read, such as {failures.Order(StringComparer.Ordinal).First()}");

        foreach ((ulong key, ulong sum) in added) existing[key] = sum;
        Checksums.Save(game.DataDir, existing);

        TableData.Save(game.DataDir, version, ResourceSizeTable.FromBinary(game.Decompress(File.ReadAllBytes(table), Path.GetFileName(table))));
        return added.Count;

        void Add(string key, string path, byte[] raw, int depth)
        {
            byte[] plain = game.Decompress(raw, path);
            ulong sum = Checksums.Hash(plain);
            if (older.Length == 0) added[Checksums.Key(key)] = sum;
            else if (Checksums.Find(existing, key, version - 1, older) != sum) added[Checksums.Key($"{key}#{version}")] = sum;

            if (!game.Opens(path, plain, depth)) return;
            foreach (SarcEntry entry in SarcFile.FromBinary(plain).Entries)
                Add($"{key}/{entry.Name}", entry.Name, entry.Data, depth + 1);
        }
    }
}
