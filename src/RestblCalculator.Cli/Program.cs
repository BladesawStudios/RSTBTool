using RestblCalculator.Core;

const string Usage = """
    usage: restbl <mod or folder of mods> [--game id] [--version 1.4.3] [--dry-run]
           restbl import <game id> <version> <folder[|prefix]>... [--out data folder]
      games: totk, oot, botw-switch, botw-wiiu. Every mod in a folder gets its own table. The game is worked out from the mods
      when it can be, and the version defaults to the one a mod already has a RESTBL for, else the latest. Import folders overlay
      in order, oldest version first.
    """;

List<string> rest = [.. args];
try
{
    string? output = Option("--out");
    if (rest.Count > 0 && rest[0] == "import") return Import(output);

    bool dryRun = rest.Remove("--dry-run");
    string? gameId = Option("--game");
    int? version = Option("--version") is { } text ? ParseVersion(text) : null;

    if (rest.Count != 1 || rest[0] is "-h" or "--help")
    {
        Console.Error.WriteLine(Usage);
        return 1;
    }

    gameId ??= Game.Detect(rest[0]);
    if (gameId is null)
    {
        Console.Error.WriteLine("Can't tell which game this mod is for; pass --game.");
        return 1;
    }

    Game game = FindGame(gameId, null);
    RestblGenerator generator = new(game);
    if (version is { } wanted && !generator.Versions.Contains(wanted))
    {
        Console.Error.WriteLine($"No vanilla RESTBL for {game.Name} {RestblGenerator.VersionName(wanted)}. Known: "
                                + string.Join(", ", generator.Versions.Reverse().Select(RestblGenerator.VersionName)));
        return 1;
    }

    IReadOnlyList<string> mods = game.FindMods(rest[0]);
    if (mods.Count == 1)
    {
        RestblResult result = generator.Generate(mods[0], version ?? generator.DefaultVersion(game.FindRoots(mods[0]).Main), write: !dryRun);

        foreach (RestblEntry e in result.Entries)
            Console.WriteLine($"{(e.Removed ? "-" : e.Added ? "+" : e.Grown ? "^" : "=")} {e.Name}  {e.Vanilla?.ToString() ?? "-"} -> {(e.Removed ? "-" : e.Size)}");
        foreach (string warning in result.Warnings) Console.Error.WriteLine($"warning: {warning}");

        Console.WriteLine(Summary(game, result));
        Console.WriteLine(Outcome(result));
        return 0;
    }

    IReadOnlyList<ModResult> results = generator.GenerateAll(mods, version, write: !dryRun);
    foreach (ModResult r in results)
    {
        string name = Path.GetRelativePath(rest[0], r.Mod);
        if (r.Error is not null) Console.Error.WriteLine($"{name}: failed: {r.Error}");
        else if (r.Skipped is not null) Console.WriteLine($"{name}: skipped: {r.Skipped}");
        else
        {
            Console.WriteLine($"{name}: {Summary(game, r.Result!)} {Outcome(r.Result!)}");
            foreach (string warning in r.Result!.Warnings) Console.Error.WriteLine($"{name}: warning: {warning}");
        }
    }

    Console.WriteLine($"{results.Count} mods: {results.Count(r => r.Result?.Written is not null)} tables written, "
                      + $"{results.Count(r => r.Result is { Written: null, Changed: true })} previewed, {results.Count(r => r.Result is { Changed: false })} needed none, "
                      + $"{results.Count(r => r.Skipped is not null)} skipped, {results.Count(r => r.Error is not null)} failed.");
    return results.Any(r => r.Error is not null) ? 1 : 0;
}
catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

static string Summary(Game game, RestblResult result)
    => $"{result.Modified} of {result.Files} files aren't vanilla {game.Name} {RestblGenerator.VersionName(result.Version)}: "
       + $"{result.Entries.Count(e => e.Added)} added, {result.Entries.Count(e => e.Grown)} grown, "
       + $"{result.Entries.Count(e => e.Removed)} removed, {result.Entries.Count(e => !e.Added && !e.Grown && !e.Removed)} already big enough.";

static string Outcome(RestblResult result)
    => result.Written is { } written ? $"Wrote {written}"
        : result.Changed ? "Dry run: nothing written."
        : "No RESTBL needed: every entry fits the vanilla one.";

int Import(string? dataRoot)
{
    if (rest.Count < 4)
    {
        Console.Error.WriteLine(Usage);
        return 1;
    }

    Game game = FindGame(rest[1], dataRoot);
    (string, string)[] roots = rest.Skip(3).Select(r => r.Split('|')).Select(r => (r[0], r.Length > 1 ? r[1] : "")).ToArray();
    int added = VanillaImporter.Import(game, ParseVersion(rest[2]), roots);
    Console.WriteLine($"Added {added} checksums to {game.DataDir}");
    return 0;
}

string? Option(string name)
{
    int at = rest.IndexOf(name);
    if (at < 0) return null;
    if (at + 1 >= rest.Count) throw new ArgumentException($"{name} needs a value.");
    string value = rest[at + 1];
    rest.RemoveRange(at, 2);
    return value;
}

static int ParseVersion(string text)
    => int.TryParse(text.Replace(".", ""), out int v) ? v : throw new ArgumentException("A version looks like 1.2.1 or 121.");

static Game FindGame(string id, string? dataRoot)
    => Game.All(dataRoot).FirstOrDefault(g => g.Id == id)
       ?? throw new ArgumentException($"Unknown game {id}. Games: {string.Join(", ", Game.All().Select(g => g.Id))}.");
