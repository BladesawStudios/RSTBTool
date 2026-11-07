using RSTBSharp;
using Yaz0Sharp;
using ZsDicSharp;

namespace RestblTool.Core;

public sealed record ModRoots(string Main, string? Dlc);

public abstract class Game(string id, string name, string? dataRoot)
{
    public string Id => id;
    public string Name => name;
    public string DataDir { get; } = Path.Combine(dataRoot ?? Path.Combine(AppContext.BaseDirectory, "Data"), id);

    public static Game[] All(string? dataRoot = null) =>
    [
        new Totk(dataRoot), new Oot(dataRoot), new Botw(wiiU: false, dataRoot), new Botw(wiiU: true, dataRoot),
    ];

    public abstract ModRoots FindRoots(string mod);

    public abstract bool IsModRoot(string dir);

    public IReadOnlyList<string> FindMods(string folder)
    {
        folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException($"{folder} doesn't exist.");

        List<string> mods = [];
        Walk(folder, 0);
        if (mods.Count > 0) return mods;
        return IsModRoot(folder) ? [folder] : throw new DirectoryNotFoundException($"No {Name} mods in {folder}.");

        void Walk(string dir, int depth)
        {
            if (IsModRoot(dir) && HoldsGameFiles(FindRoots(dir)))
            {
                mods.Add(dir);
                return;
            }

            if (depth == MaxModDepth) return;
            foreach (string sub in Directory.EnumerateDirectories(dir, "*", ModSearch).Order(StringComparer.OrdinalIgnoreCase))
                if (!Path.GetFileName(sub).StartsWith('.')) Walk(sub, depth + 1);
        }
    }

    private bool HoldsGameFiles(ModRoots roots)
        => new[] { roots.Main, roots.Dlc }.OfType<string>().Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateDirectories(root, "*", ModSearch))
            .Where(dir => GameFolders.Contains(Path.GetFileName(dir)))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0, RecurseSubdirectories = true }))
            .Any(file => !Path.GetFileName(file).Contains("sizetable", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(file).StartsWith('.'));

    protected abstract HashSet<string> GameFolders { get; }

    private const int MaxModDepth = 3;
    private static readonly EnumerationOptions ModSearch = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };

    public abstract string TableName(int version);

    public abstract ResourceSizeTable NewTable();

    public abstract string CanonicalName(string path);

    public abstract byte[] Decompress(byte[] data, string path);

    public abstract byte[] CompressTable(byte[] table);

    public abstract uint? Size(string path, byte[] raw, byte[] plain);

    public abstract bool Opens(string path, byte[] plain, int depth);

    public virtual string[] EntryNames(string canonical, bool inDlcPack, ResourceSizeTable vanilla) => [canonical];

    public virtual void PrepareImport(string dump) { }

    public static string? Detect(string mod)
    {
        mod = Path.TrimEndingDirectorySeparator(Path.GetFullPath(mod));
        if (!Directory.Exists(mod)) return null;

        string[] near = [mod, .. Directory.GetDirectories(mod), Path.GetDirectoryName(mod) ?? mod, Path.GetDirectoryName(Path.GetDirectoryName(mod) ?? mod) ?? mod];
        string[] names = near.Select(Path.GetFileName).OfType<string>().ToArray();
        if (names.Any(n => n.Contains("0100F2C0115B6000", StringComparison.OrdinalIgnoreCase))) return Totk.TotkId;
        if (names.Any(n => n.Contains("01007EF00011E000", StringComparison.OrdinalIgnoreCase) || n.Contains("01007EF00011F001", StringComparison.OrdinalIgnoreCase))) return Botw.SwitchId;

        string[] folders = Directory.GetDirectories(mod).Select(Path.GetFileName).OfType<string>().ToArray();
        bool wiiU = Path.GetFileName(mod) == "content" || folders.Contains("content") || folders.Contains("aoc");
        if (wiiU && !folders.Contains("romfs")) return Botw.WiiUId;

        foreach (string file in Directory.EnumerateFiles(mod, "*", SearchOption.AllDirectories).Take(5000))
        {
            string fileName = Path.GetFileName(file);
            if (fileName.Contains("Nin_Ounce", StringComparison.Ordinal)) return Oot.OotId;
            if (fileName.EndsWith(".srsizetable", StringComparison.Ordinal) || Botw.IsBotwFile(fileName)) return Botw.SwitchId;
            if (fileName.EndsWith(".rsizetable.zs", StringComparison.Ordinal) || fileName.Contains("Nin_NX_NVN", StringComparison.Ordinal)) return Totk.TotkId;
        }

        return null;
    }
}

public abstract class ZstdGame : Game
{
    private readonly Lazy<ZsDic> _zs;

    protected ZstdGame(string id, string name, string? dataRoot) : base(id, name, dataRoot)
        => _zs = new Lazy<ZsDic>(() => ZsDic.FromPack(File.ReadAllBytes(Path.Combine(DataDir, "ZsDic.pack.zs"))));

    public override byte[] Decompress(byte[] data, string path) => ZsDic.IsCompressed(data) ? _zs.Value.Decompress(data, path) : data;

    public override byte[] CompressTable(byte[] table) => _zs.Value.Compress(table, ZsDictionary.None);

    public override ResourceSizeTable NewTable() => new(RstbFormat.Restbl);

    public override bool Opens(string path, byte[] plain, int depth)
        => CanonicalName(path) is var name && name.EndsWith(".pack", StringComparison.Ordinal) && name != "Pack/ZsDic.pack";

    public override void PrepareImport(string dump)
    {
        string target = Path.Combine(DataDir, "ZsDic.pack.zs");
        if (!File.Exists(target)) File.Copy(Path.Combine(dump, "Pack", "ZsDic.pack.zs"), target);
    }

    public override ModRoots FindRoots(string mod)
    {
        mod = Path.TrimEndingDirectorySeparator(Path.GetFullPath(mod));
        if (!Directory.Exists(mod)) throw new DirectoryNotFoundException($"{mod} doesn't exist.");
        if (Path.GetFileName(mod).Equals("romfs", StringComparison.OrdinalIgnoreCase)) return new ModRoots(mod, null);
        if (Path.Combine(mod, "romfs") is var direct && Directory.Exists(direct)) return new ModRoots(direct, null);

        string[] nested = Directory.GetDirectories(mod).Select(d => Path.Combine(d, "romfs")).Where(Directory.Exists).ToArray();
        return nested.Length switch
        {
            1 => new ModRoots(nested[0], null),
            0 => throw new DirectoryNotFoundException($"No romfs folder in {mod}."),
            _ => throw new InvalidDataException($"More than one romfs folder in {mod}; pick one."),
        };
    }

    public override bool IsModRoot(string dir)
        => Path.GetFileName(dir).Equals("romfs", StringComparison.OrdinalIgnoreCase) || Directory.Exists(Path.Combine(dir, "romfs"));
}

public sealed class Totk(string? dataRoot) : ZstdGame(TotkId, "Tears of the Kingdom", dataRoot)
{
    public const string TotkId = "totk";

    protected override HashSet<string> GameFolders { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "AI", "AISchedule", "AS", "ActorSystem", "AnimationEvent", "BSA", "Bake", "Banc", "CameraAnimation", "Cave", "CaveBounding", "Chemical",
        "Component", "Cooking", "Cull", "ELink2", "Ecosystem", "Effect", "Env", "Event", "Font", "Forest", "Game", "GameBalance", "GameData",
        "GameParameter", "GrassStats", "Lib", "Life", "Logic", "Mals", "Model", "Movie", "Pack", "Perception", "Phive", "Preload", "PrequelSaveData",
        "RSDB", "Rumble", "SLink2", "Scene", "Sequence", "Shader", "Sound", "System", "TerrainArc", "TexToGo", "UI", "UMii", "Voice", "VolumeStats", "WorldMgr",
    };

    public override string TableName(int version)
        => version >= 140 ? $"ResourceSizeTable.Product.{version}.Nin_NX_NVN.rsizetable.zs" : $"ResourceSizeTable.Product.{version}.rsizetable.zs";

    public override string CanonicalName(string path) => TotkSizeCalculator.CanonicalName(path);

    public override uint? Size(string path, byte[] raw, byte[] plain)
        => TotkSizeCalculator.Calculate(path, path.EndsWith(".ta.zs", StringComparison.Ordinal) || path.EndsWith(".mc", StringComparison.Ordinal) ? raw : plain) is var size and not 0 ? size : null;
}

public sealed class Oot(string? dataRoot) : ZstdGame(OotId, "Ocarina of Time", dataRoot)
{
    public const string OotId = "oot";

    protected override HashSet<string> GameFolders { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "AI", "AISchedule", "AS", "ActorSystem", "AnimationEvent", "AoC1", "BSA", "Bake", "Banc", "CameraAnimation", "Chemical", "Component", "ELink2",
        "Effect", "Env", "Event", "Foliage", "Font2", "Game", "GameBalance", "GameData", "GameParameter", "Layout2Shader", "Lib", "Life", "Logic", "Mals",
        "Model", "Movie", "Pack", "Perception", "Phive", "PreComputedCulling", "RSDB", "Reaction", "Rumble", "SLink2", "Scene", "Sequence", "Shader", "Sound",
        "System", "TerrainArc", "Tex", "UI", "Voice", "VolumeStats", "WorldMgr", "agl", "gsys",
    };

    public override string TableName(int version) => $"ResourceSizeTable.Product.{version}.Nin_Ounce_NVN2.rsizetable.zs";

    public override string CanonicalName(string path) => OotSizeCalculator.CanonicalName(path);

    public override uint? Size(string path, byte[] raw, byte[] plain)
        => OotSizeCalculator.Calculate(path, plain) is var size and not 0 ? size : null;
}

public sealed class Botw(bool wiiU, string? dataRoot) : Game(wiiU ? WiiUId : SwitchId, wiiU ? "Breath of the Wild (Wii U)" : "Breath of the Wild (Switch)", dataRoot)
{
    public const string SwitchId = "botw-switch";
    public const string WiiUId = "botw-wiiu";

    protected override HashSet<string> GameFolders { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Actor", "Aoc", "Effect", "Event", "Font", "Game", "Layout", "Local", "Map", "Model", "Movie", "NavMesh", "Pack", "Physics", "Sound", "System",
        "Terrain", "UI", "Voice",
    };

    private const string Dlc = "Aoc/0010/";
    private const string SwitchBase = "01007EF00011E000";
    private const string SwitchDlc = "01007EF00011F001";

    private static readonly HashSet<string> NoEntry =
    [
        ".agstats", ".baacl", ".baatn", ".baglblm", ".baglccr", ".baglclwd", ".baglcube", ".bagldof", ".baglenv", ".baglenvset", ".baglfila",
        ".bagllmap", ".bagllref", ".baglmf", ".baglshpp", ".baroc", ".bffnt", ".bflan", ".bflyt", ".bfstm", ".bgapkginfo", ".bgapkglist",
        ".bgdata", ".bgenv", ".bglght", ".bgmsconf", ".bgsdw", ".bgsvdata", ".bksky", ".bofx", ".bptclconf", ".bvege", ".extm", ".genvres",
        ".gtx", ".harcb", ".hght", ".kybin", ".mate", ".msbt", ".pack", ".raw", ".stera",
    ];

    private static readonly HashSet<string> NoEntrySwitch = [".bnsh", ".bntx", ".bushvt", ".gsh"];
    private static readonly HashSet<string> NoEntryWiiU = [".bflim", ".bgsh"];

    public static bool IsBotwFile(string name)
        => Path.GetExtension(name) is ".sbactorpack" or ".sbfres" or ".sbitemico" or ".sblarc" or ".ssarc" or ".sbyml" or ".smubin" or ".sbeventpack" or ".shktmrb" or ".shknm2" or ".sbmaptex" or ".sbars" or ".sstats";

    public override string TableName(int version) => "ResourceSizeTable.product.srsizetable";

    public override ResourceSizeTable NewTable() => new(RstbFormat.Rstb, bigEndian: wiiU);

    public override string CanonicalName(string path) => BotwSizeCalculator.CanonicalName(path);

    public override byte[] Decompress(byte[] data, string path) => Yaz0.DecompressIfNeeded(data);

    public override byte[] CompressTable(byte[] table) => Yaz0.Compress(table);

    public override uint? Size(string path, byte[] raw, byte[] plain)
    {
        string canonical = CanonicalName(path);
        string ext = Path.GetExtension(canonical);
        if (canonical.EndsWith("Actor/ActorInfo.product.byml", StringComparison.Ordinal) || NoEntry.Contains(ext) || (wiiU ? NoEntryWiiU : NoEntrySwitch).Contains(ext)) return null;
        return BotwSizeCalculator.Calculate(path, raw, wiiU);
    }

    public override bool Opens(string path, byte[] plain, int depth)
        => depth < 4 && plain.AsSpan().StartsWith("SARC"u8) && !path.EndsWith(".ssarc", StringComparison.Ordinal);

    public override string[] EntryNames(string canonical, bool inDlcPack, ResourceSizeTable vanilla)
    {
        if (!inDlcPack) return [canonical];
        string prefixed = Dlc + canonical;
        bool hasPrefixed = vanilla.Contains(prefixed), hasPlain = vanilla.Contains(canonical);
        if (hasPrefixed || hasPlain) return hasPrefixed && hasPlain ? [prefixed, canonical] : hasPrefixed ? [prefixed] : [canonical];
        return canonical.StartsWith("Map/", StringComparison.Ordinal) || canonical.StartsWith("NavMesh/", StringComparison.Ordinal) || canonical.StartsWith("Physics/", StringComparison.Ordinal)
            ? [prefixed]
            : [canonical];
    }

    public override ModRoots FindRoots(string mod)
    {
        mod = Path.TrimEndingDirectorySeparator(Path.GetFullPath(mod));
        if (!Directory.Exists(mod)) throw new DirectoryNotFoundException($"{mod} doesn't exist.");

        if (Path.GetFileName(mod).Equals(wiiU ? "content" : "romfs", StringComparison.OrdinalIgnoreCase))
        {
            string? parent = Path.GetDirectoryName(mod);
            string? grandparent = Path.GetDirectoryName(parent);
            ModRoots? around = Look(parent) is { } a && SamePath(a.Main, mod) ? a : Look(grandparent) is { } b && SamePath(b.Main, mod) ? b : null;
            return around ?? new ModRoots(mod, null);
        }

        if (Look(mod) is { } here) return here;

        ModRoots[] nested = Directory.GetDirectories(mod).Select(Look).OfType<ModRoots>().ToArray();
        return nested.Length switch
        {
            1 => nested[0],
            0 => throw new DirectoryNotFoundException($"No {(wiiU ? "content or aoc" : "romfs")} folder in {mod}."),
            _ => throw new InvalidDataException($"More than one mod in {mod}; pick one."),
        };
    }

    public override bool IsModRoot(string dir)
        => Path.GetFileName(dir).Equals(wiiU ? "content" : "romfs", StringComparison.OrdinalIgnoreCase) || Look(dir) is not null;

    private ModRoots? Look(string? dir)
    {
        if (dir is null) return null;
        string? main, dlc;
        string fallback;
        if (wiiU)
        {
            fallback = Path.Combine(dir, "content");
            main = Directory.Exists(fallback) ? fallback : null;
            dlc = new[] { Path.Combine(dir, "aoc", "0010"), Path.Combine(dir, "aoc", "content", "0010") }.FirstOrDefault(Directory.Exists);
        }
        else
        {
            fallback = Path.Combine(dir, SwitchBase, "romfs");
            main = new[] { Path.Combine(dir, "romfs"), fallback }.FirstOrDefault(Directory.Exists);
            string title = Path.Combine(dir, SwitchDlc, "romfs");
            dlc = Directory.Exists(title) ? title : null;
        }

        return main is null && dlc is null ? null : new ModRoots(main ?? fallback, dlc);
    }

    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
