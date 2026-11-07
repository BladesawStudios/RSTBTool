using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using RestblTool.Core;
using RestblTool.Gui.UI;

namespace RestblTool.Gui;

public sealed class App : IDisposable
{
    private readonly ToolConfig _config = ToolConfig.Load();
    private string _mod;

    private readonly Game[] _games = Game.All();
    private int _game;

    private readonly Dictionary<string, Task<RestblGenerator>> _loads = [];
    private RestblGenerator? _generator;
    private string? _loadError;

    private string? _modDetected;
    private string? _modChecked;
    private string? _modCheckedGame;
    private string? _modRomfs;
    private string? _modError;
    private int[] _modVersions = [];

    private int _version;

    private Task<RestblResult>? _run;
    private (int Done, int Total) _progress;
    private RestblResult? _result;
    private string? _error;

    private int _filter;
    private string _search = "";
    private RestblEntry[] _view = [];

    private Task<string?>? _picking;
    private Action<string>? _picked;

    public App(string[] args)
    {
        _mod = _config.Mod ?? "";
        _game = Math.Max(0, Array.FindIndex(_games, g => g.Id == _config.Game));
        Drop(args);
    }

    public void Drop(string[] paths)
    {
        if (paths.FirstOrDefault(Directory.Exists) is { } path) _mod = path;
    }

    public void Draw()
    {
        CollectPicker();
        DetectGame();
        CollectLoad();
        CheckMod();
        CollectRun();

        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Theme.Px(18), Theme.Px(16)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.Begin("##main", Widgets.FixedPanel | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar(2);

        Widgets.DialogTitle("RESTBL Generator", "Builds the resource size table a mod needs, from the vanilla data that ships with the app.");

        bool busy = _run is not null;
        ImGui.BeginDisabled(busy);
        (string modNote, Vector4 modColour) = _modError is not null ? (_modError, Theme.Error)
            : ($"Writes to {Path.Combine(_modRomfs ?? "", "System", "Resource")}", Theme.TextMuted);
        if (PathRow("Mod", "##mod", ref _mod, "The mod folder, or its romfs", modNote, modColour))
            Pick(_mod, p => _mod = p);
        GameRow();
        ImGui.EndDisabled();

        ImGui.Dummy(new Vector2(0, Theme.Px(4)));
        Actions();
        ImGui.Dummy(new Vector2(0, Theme.Px(6)));
        Results();

        ImGui.End();
    }

    private static bool PathRow(string label, string id, ref string value, string hint, string note, Vector4 noteColour)
    {
        ImGui.PushID(id);
        Widgets.Bold(label);
        float button = ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - button - ImGui.GetStyle().ItemSpacing.X);
        ImGui.PushFont(Fonts.Mono);
        ImGui.InputTextWithHint(id, hint, ref value, 1024);
        ImGui.PopFont();
        ImGui.SameLine();
        bool browse = Widgets.IconButton(Icon.FolderOpen, "Browse...");
        Widgets.Caption(note, noteColour);
        ImGui.Dummy(new Vector2(0, Theme.Px(4)));
        ImGui.PopID();
        return browse;
    }

    private void GameRow()
    {
        Widgets.Bold("Game and version");
        ImGui.SetNextItemWidth(Theme.Px(240));
        int game = Widgets.Combo("##game", _game, _games.Select(g => g.Name).ToArray());
        if (game != _game) SelectGame(game);
        ImGui.SameLine();

        if (_generator is null)
        {
            ImGui.AlignTextToFramePadding();
            Widgets.Caption(_loadError ?? "Loading the vanilla tables and checksums...", _loadError is null ? Theme.Accent : Theme.Error);
            ImGui.Dummy(new Vector2(0, Theme.Px(4)));
            return;
        }

        int[] versions = [.. _generator.Versions];
        ImGui.SetNextItemWidth(Theme.Px(110));
        int picked = Widgets.Combo("##version", Array.IndexOf(versions, _version), versions.Select(RestblGenerator.VersionName).ToArray());
        if (picked >= 0) _version = versions[picked];

        Widgets.Caption(_modVersions.Length > 0
            ? $"{_generator.TableName(_version)}  ·  The mod has tables for {string.Join(", ", _modVersions.Select(RestblGenerator.VersionName))}."
            : _generator.TableName(_version));
        ImGui.Dummy(new Vector2(0, Theme.Px(4)));
    }

    private void SelectGame(int game)
    {
        _game = game;
        _generator = null;
        _result = null;
        _error = null;
        _view = [];
    }

    private void DefaultVersion()
    {
        if (_generator is null) return;
        int[] known = _modVersions.Where(_generator.Versions.Contains).ToArray();
        _version = known.Length == 1 ? known[0]
            : _generator.Versions.Contains(_version) ? _version
            : _config.Version is { } last && _generator.Versions.Contains(last) ? last
            : _generator.Latest;
    }

    private void Actions()
    {
        bool ready = _generator is not null && _modRomfs is not null && _run is null;
        ImGui.BeginDisabled(!ready);
        if (Widgets.PrimaryButton("Generate RESTBL")) Start(write: true);
        Widgets.Tip("Writes the table into the mod's romfs/System/Resource, replacing the one for this version if there is one.");
        ImGui.SameLine();
        if (ImGui.Button("Preview")) Start(write: false);
        Widgets.Tip("Works out the entries without writing anything.");
        ImGui.EndDisabled();

        if (_run is null) return;
        ImGui.SameLine();
        (int done, int total) = _progress;
        ImGui.ProgressBar(total == 0 ? 0f : (float)done / total, new Vector2(-1, ImGui.GetFrameHeight()),
            total == 0 ? "Listing files..." : $"{done:N0} / {total:N0} files");
    }

    private void Results()
    {
        if (_error is not null) Widgets.Banner(Icon.Warning, _error, Theme.Error);

        if (_result is not { } r)
        {
            if (_error is null)
                Widgets.EmptyState("Nothing generated yet", "Pick a mod and its game version, then Generate. You can also drop the mod folder onto the window.");
            return;
        }

        int added = r.Entries.Count(e => e.Added), grown = r.Entries.Count(e => e.Grown), removed = r.Entries.Count(e => e.Removed), kept = r.Entries.Count - added - grown - removed;
        string counts = $"{r.Modified:N0} of {r.Files:N0} files aren't vanilla {RestblGenerator.VersionName(r.Version)}: {added:N0} added, {grown:N0} grown, {(removed > 0 ? $"{removed:N0} removed, " : "")}{kept:N0} already big enough.";
        if (r.Written is { } written)
        {
            Widgets.Banner(Icon.Check, $"Wrote {Path.GetRelativePath(Path.GetDirectoryName(r.Romfs)!, written)}. {counts}", Theme.Good);
            if (ImGui.Button("Show in Explorer")) Process.Start("explorer.exe", $"/select,\"{written}\"");
            ImGui.Dummy(new Vector2(0, Theme.Px(2)));
        }
        else if (!r.Changed)
            Widgets.Banner(Icon.Info, $"No RESTBL needed: every file fits its vanilla entry, so nothing was written. {counts}", Theme.TextMuted);
        else
            Widgets.Banner(Icon.Info, $"Preview only, nothing written. {counts}", Theme.Accent);

        if (r.Warnings.Count > 0)
        {
            const int shown = 6;
            string more = r.Warnings.Count > shown ? $"\n...and {r.Warnings.Count - shown} more." : "";
            Widgets.Banner(Icon.Warning, string.Join("\n", r.Warnings.Take(shown)) + more, Theme.Warning);
        }

        if (r.Entries.Count == 0) return;

        string[] filters = [$"All  {r.Entries.Count:N0}", $"Added  {added:N0}", $"Grown  {grown:N0}", $"Kept  {kept:N0}"];
        string[] filterTips = ["", "New resources vanilla has no entry for", "Entries raised above vanilla", "Changed files whose vanilla entry is already big enough"];
        if (removed > 0)
        {
            filters = [.. filters, $"Removed  {removed:N0}"];
            filterTips = [.. filterTips, "Changed files whose size can't be worked out, so their entries are taken out"];
        }

        int filter = Widgets.Segmented("##filter", _filter, filters, Theme.Px(removed > 0 ? 520 : 420), filterTips);
        ImGui.SameLine();
        bool searched = Widgets.Search("##search", ref _search, "Filter by name", 256);
        if (filter != _filter || searched)
        {
            _filter = filter;
            RebuildView();
        }

        EntryTable();
    }

    private unsafe void EntryTable()
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersOuter
                                      | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit;
        if (!ImGui.BeginTable("##entries", 4, flags, new Vector2(0, ImGui.GetContentRegionAvail().Y))) return;

        float number = ImGui.CalcTextSize("000,000,000").X + Theme.Px(8);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Resource", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Vanilla", ImGuiTableColumnFlags.WidthFixed, number);
        ImGui.TableSetupColumn("Calculated", ImGuiTableColumnFlags.WidthFixed, number);
        ImGui.TableSetupColumn("Written", ImGuiTableColumnFlags.WidthFixed, number);
        ImGui.TableHeadersRow();

        ImGuiListClipperPtr clipper = new(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(_view.Length);
        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                RestblEntry e = _view[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                Widgets.Mono(e.Name, e.Added ? Theme.Good : e.Grown ? Theme.Text : Theme.TextMuted);
                ImGui.TableNextColumn();
                Number(e.Vanilla, Theme.TextMuted);
                ImGui.TableNextColumn();
                Number(e.Calculated, Theme.TextMuted);
                ImGui.TableNextColumn();
                Number(e.Removed ? null : e.Size, e.Added || e.Grown ? Theme.Text : Theme.TextMuted);
            }
        }
        clipper.End();
        clipper.Destroy();

        ImGui.EndTable();
    }

    private static void Number(uint? value, Vector4 colour)
    {
        string text = value?.ToString("N0") ?? "-";
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X);
        ImGui.TextColored(colour, text);
    }

    private void RebuildView()
    {
        IEnumerable<RestblEntry> entries = _result?.Entries ?? [];
        entries = _filter switch
        {
            1 => entries.Where(e => e.Added),
            2 => entries.Where(e => e.Grown),
            3 => entries.Where(e => !e.Added && !e.Grown && !e.Removed),
            4 => entries.Where(e => e.Removed),
            _ => entries,
        };
        if (_search.Length > 0) entries = entries.Where(e => e.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));
        _view = entries.ToArray();
    }

    private void DetectGame()
    {
        if (_mod == _modDetected) return;
        _modDetected = _mod;
        try
        {
            if (Game.Detect(_mod) is { } id && Array.FindIndex(_games, g => g.Id == id) is var found and >= 0 && found != _game) SelectGame(found);
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
        }
    }

    private void CollectLoad()
    {
        Game game = _games[_game];
        if (!_loads.TryGetValue(game.Id, out Task<RestblGenerator>? load))
            _loads[game.Id] = load = Task.Run(() => new RestblGenerator(game));

        RestblGenerator? was = _generator;
        _generator = load.IsCompletedSuccessfully ? load.Result : null;
        _loadError = load.IsFaulted ? load.Exception!.InnerException!.Message : null;
        if (_generator is not null && _generator != was) DefaultVersion();
    }

    private void CheckMod()
    {
        if (_mod == _modChecked && _games[_game].Id == _modCheckedGame) return;
        _modChecked = _mod;
        _modCheckedGame = _games[_game].Id;
        _modRomfs = null;
        _modError = null;
        _modVersions = [];

        if (_mod.Length == 0)
        {
            _modError = "Pick the mod folder. Its romfs is found inside it.";
            return;
        }

        try
        {
            _modRomfs = _games[_game].FindRoots(_mod).Main;
            _modVersions = RestblGenerator.ModVersions(_modRomfs);
            DefaultVersion();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            _modError = e.Message;
        }
    }

    private void Start(bool write)
    {
        _config.Game = _games[_game].Id;
        _config.Mod = _mod;
        _config.Version = _version;
        _config.Save();

        RestblGenerator generator = _generator!;
        string mod = _mod;
        int version = _version;
        _progress = default;
        _error = null;
        _run = Task.Run(() => generator.Generate(mod, version, write, new Progress(p => _progress = p)));
    }

    private void CollectRun()
    {
        if (_run is not { IsCompleted: true } run) return;
        _run = null;

        if (run.IsCompletedSuccessfully)
        {
            _result = run.Result;
            if (_filter == 4 && !_result.Entries.Any(e => e.Removed)) _filter = 0;
            _modChecked = null;
            RebuildView();
        }
        else
        {
            _result = null;
            _error = run.Exception!.InnerException!.Message;
        }
    }

    private void Pick(string initial, Action<string> then)
    {
        if (_picking is not null) return;
        _picking = NativeDialogs.PickFolder(initial);
        _picked = then;
    }

    private void CollectPicker()
    {
        if (_picking is not { IsCompleted: true } picking) return;
        _picking = null;
        if (picking.IsCompletedSuccessfully && picking.Result is { } path) _picked?.Invoke(path);
        _picked = null;
    }

    public void Dispose()
    {
        _run?.Wait();
    }

    private sealed class Progress(Action<(int Done, int Total)> report) : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value) => report(value);
    }
}
