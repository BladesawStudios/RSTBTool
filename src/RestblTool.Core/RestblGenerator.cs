using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using RSTBSharp;
using SarcSharp;

namespace RestblTool.Core;

public sealed record RestblEntry(string Name, uint? Vanilla, uint Calculated, uint Size)
{
    public bool Removed => Size == 0;
    public bool Added => Vanilla is null && !Removed;
    public bool Grown => Size > Vanilla;
}

public sealed record RestblResult(int Version, string Romfs, string? Written, IReadOnlyList<RestblEntry> Entries, IReadOnlyList<string> Warnings, int Files, int Modified)
{
    public bool Changed => Entries.Any(e => e.Added || e.Grown || e.Removed);
}

public sealed partial class RestblGenerator
{
    private readonly Game _game;
    private readonly Dictionary<ulong, ulong> _checksums;
    private readonly int[] _versions;
    private readonly ConcurrentDictionary<int, byte[]> _vanilla = new();

    public Game Game => _game;
    public IReadOnlyList<int> Versions => _versions;
    public int Latest => _versions[0];

    public RestblGenerator(Game game)
    {
        _game = game;
        _versions = TableData.Versions(game.DataDir);
        if (_versions.Length == 0) throw new FileNotFoundException($"No vanilla data for {game.Name} in {game.DataDir}.");
        _checksums = Checksums.Load(game.DataDir);
    }

    public static string VersionName(int version) => $"{version / 100}.{version / 10 % 10}.{version % 10}";

    public string TableName(int version) => _game.TableName(version);

    public static int? FileVersion(string path)
        => ProductVersion().Match(Path.GetFileName(path)) is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;

    public static int[] ModVersions(string romfs)
    {
        string resource = Path.Combine(romfs, "System", "Resource");
        return Directory.Exists(resource)
            ? Directory.GetFiles(resource, "ResourceSizeTable.Product.*.rsizetable.zs").Select(FileVersion).OfType<int>().Order().ToArray()
            : [];
    }

    public int DefaultVersion(string romfs)
        => ModVersions(romfs).Where(_versions.Contains).ToArray() is [var only] ? only : Latest;

    public RestblResult Generate(string mod, int version, bool write = true, IProgress<(int Done, int Total)>? progress = null)
    {
        if (!_versions.Contains(version))
            throw new InvalidOperationException($"No vanilla RESTBL for {_game.Name} {VersionName(version)}.");
        ModRoots roots = _game.FindRoots(mod);

        (string Path, string Full)[] files = Enumerate(roots.Main, "").Concat(roots.Dlc is null ? [] : Enumerate(roots.Dlc, "Aoc/0010/"))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToArray();

        List<string> warnings = [];
        if (Game.Detect(mod) is { } detected && detected != _game.Id)
            warnings.Add($"This looks like a {Game.All().First(g => g.Id == detected).Name} mod, but the table is for {_game.Name}.");

        (string Path, string Full)[] visit = files.Where(f => FileVersion(f.Path) is not { } v || v == version).ToArray();
        foreach (IGrouping<int, string> other in files.Except(visit)
                     .Where(f => !f.Path.EndsWith(".rsizetable.zs", StringComparison.Ordinal))
                     .GroupBy(f => FileVersion(f.Path)!.Value, f => f.Path))
        {
            warnings.Add($"Skipped {other.Count()} file{(other.Count() == 1 ? "" : "s")} for {VersionName(other.Key)} "
                         + $"({string.Join(", ", other.Take(3))}{(other.Count() > 3 ? ", ..." : "")}), which {VersionName(version)} doesn't load.");
        }

        byte[] vanillaTable = _vanilla.GetOrAdd(version, _ => TableData.Load(_game, version).ToBinary());
        ResourceSizeTable vanilla = ResourceSizeTable.FromBinary(vanillaTable);

        Scan scan = new(version, vanilla);
        int done = 0, modified = 0;
        Parallel.ForEach(visit, file =>
        {
            try
            {
                if (Visit(scan, file.Path, file.Path, File.ReadAllBytes(file.Full), false, 0)) Interlocked.Increment(ref modified);
            }
            catch (Exception e)
            {
                scan.Failures.Add($"{file.Path} was skipped: {e.Message}");
            }

            progress?.Report((Interlocked.Increment(ref done), visit.Length));
        });
        warnings.AddRange(scan.Failures.Order(StringComparer.Ordinal));

        ResourceSizeTable table = ResourceSizeTable.FromBinary(vanillaTable);
        List<RestblEntry> entries = [];
        foreach ((string name, uint calculated) in scan.Sizes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            uint? old = vanilla.TryGetSize(name, out uint v) ? v : null;
            if (calculated == 0)
            {
                if (old is not null)
                {
                    table.Remove(name);
                    entries.Add(new RestblEntry(name, old, 0, 0));
                }

                continue;
            }

            uint size = Math.Max(calculated, old ?? 0);
            entries.Add(new RestblEntry(name, old, calculated, size));

            if (!table.TryGetSize(name, out uint current) || current < size) table.SetSize(name, size);
        }

        RestblResult result = new(version, roots.Main, null, entries, warnings, files.Length, modified);
        if (!write || !result.Changed) return result;

        string output = Path.Combine(roots.Main, "System", "Resource", TableName(version));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllBytes(output, _game.CompressTable(table.ToBinary()));
        return result with { Written = output };
    }

    private static IEnumerable<(string Path, string Full)> Enumerate(string root, string prefix)
        => Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => (prefix + Path.GetRelativePath(root, f).Replace('\\', '/'), f))
            : [];

    private bool Visit(Scan scan, string key, string path, byte[] raw, bool inDlcPack, int depth)
    {
        string canonical = _game.CanonicalName(path);
        if (canonical.EndsWith(".rsizetable", StringComparison.Ordinal)) return false;

        byte[] plain = _game.Decompress(raw, path);
        if (Checksums.Find(_checksums, key, scan.Version, _versions) == Checksums.Hash(plain)) return false;

        if (_game.Size(path, raw, plain) is { } size)
            foreach (string name in _game.EntryNames(canonical, inDlcPack, scan.Vanilla))
                scan.Sizes.AddOrUpdate(name, size, (_, other) => Math.Max(other, size));

        if (!_game.Opens(path, plain, depth)) return true;

        bool dlcPack = depth == 0 && key.StartsWith("Aoc/0010/", StringComparison.Ordinal) && canonical.EndsWith(".pack", StringComparison.Ordinal);
        foreach (SarcEntry entry in SarcFile.FromBinary(plain).Entries)
        {
            try
            {
                Visit(scan, $"{key}/{entry.Name}", entry.Name, entry.Data, dlcPack, depth + 1);
            }
            catch (Exception e)
            {
                scan.Failures.Add($"{key}/{entry.Name} was skipped: {e.Message}");
            }
        }

        return true;
    }

    private sealed record Scan(int Version, ResourceSizeTable Vanilla)
    {
        public ConcurrentDictionary<string, uint> Sizes { get; } = new(StringComparer.Ordinal);
        public ConcurrentBag<string> Failures { get; } = [];
    }

    [GeneratedRegex(@"\.Product\.(\d{3})\.")]
    private static partial Regex ProductVersion();
}
