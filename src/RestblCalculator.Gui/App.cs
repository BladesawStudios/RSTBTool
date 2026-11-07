using System.Numerics;
using ImGuiNET;
using RestblCalculator.Core;
using RestblCalculator.Gui.UI;

namespace RestblCalculator.Gui;

public sealed class App : IDisposable
{
    private static readonly (string Label, float? Scale)[] UiSizes =
    [
        ("Auto", null), ("100%", 1f), ("125%", 1.25f), ("150%", 1.5f), ("175%", 1.75f), ("200%", 2f), ("250%", 2.5f),
    ];

    private readonly ToolConfig _config = ToolConfig.Load();
    private string _mod;
    private string _folder;

    private readonly Game[] _games = Game.All();
    private int _game;

    private readonly Dictionary<string, Task<RestblGenerator>> _loads = [];
    private RestblGenerator? _generator;
    private string? _loadError;

    private string? _modDetected;
    private string? _folderDetected;
    private string? _checkedGame;
    private string? _modChecked;
    private string[] _modList = [];
    private string _modWrites = "";
    private string? _modError;
    private int[] _modVersions = [];
    private string? _folderChecked;
    private string[] _folderList = [];
    private string? _folderError;
    private int[] _folderVersions = [];

    private int _version;

    private Task<IReadOnlyList<ModResult>>? _run;
    private string[] _runMods = [];
    private string _runRoot = "";
    private int _runMod;
    private bool _runWrote;
    private (int Done, int Total) _progress;
    private IReadOnlyList<ModResult>? _results;
    private int _selected;
    private RestblResult? _result;
    private string? _error;

    private int _filter;
    private string _search = "";
    private RestblEntry[] _view = [];

    private Task<string?>? _picking;
    private Action<string>? _picked;

    public App(string[] args)
    {
        _mod = _modDetected = _config.Mod ?? "";
        _folder = _folderDetected = _config.ModsFolder ?? "";
        _game = Math.Max(0, Array.FindIndex(_games, g => g.Id == _config.Game));
        Drop(args);
    }

    public float? UiScale => _config.UiScale;

    public void Drop(string[] paths)
    {
        foreach (string path in paths.Where(Directory.Exists))
        {
            try
            {
                if (Game.Detect(path) is { } id && Array.FindIndex(_games, g => g.Id == id) is var found and >= 0 && found != _game) SelectGame(found);
                if (_games[_game].FindMods(path).Count > 1) _folder = path;
                else _mod = path;
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
            {
                _mod = path;
            }
        }
    }

    public void Draw()
    {
        CollectPicker();
        DetectGame();
        CollectLoad();
        CheckMods();
        CollectRun();

        UiSizeShortcuts();

        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Theme.Px(18), Theme.Px(16)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.Begin("##main", Widgets.FixedPanel);
        ImGui.PopStyleVar(2);

        UiSizeControl();
        Widgets.DialogTitle("RESTBL Calculator");

        bool busy = _run is not null;
        ImGui.BeginDisabled(busy);
        (string modNote, Vector4 modColour) = _modError is not null ? (_modError, Theme.Error)
            : _modList.Length == 1 ? ($"Writes to {_modWrites}", Theme.TextMuted)
            : ("", Theme.TextFaint);
        if (PathRow("Mod", "##mod", ref _mod, "A mod folder, or its romfs", modNote, modColour))
            Pick(_mod, p => _mod = p);

        (string folderNote, Vector4 folderColour) = _folderError is not null ? (_folderError, Theme.Error)
            : _folderList.Length > 0 ? ($"Found {_folderList.Length} {(_folderList.Length == 1 ? "mod" : "mods")}. Each gets its own table, in its own System/Resource folder.", Theme.TextMuted)
            : ("", Theme.TextFaint);
        if (PathRow("Folder of mods", "##folder", ref _folder, "A folder with mods in it", folderNote, folderColour))
            Pick(_folder, p => _folder = p);

        GameRow();
        ImGui.EndDisabled();

        ImGui.Dummy(new Vector2(0, Theme.Px(4)));
        Actions();
        ImGui.Dummy(new Vector2(0, Theme.Px(6)));
        Results();

        ImGui.End();
    }

    private void UiSizeControl()
    {
        const string label = "UI size";
        Vector2 after = ImGui.GetCursorPos();
        float width = Theme.Px(84);
        ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - width - ImGui.CalcTextSize(label).X - ImGui.GetStyle().ItemSpacing.X, after.Y));
        ImGui.AlignTextToFramePadding();
        Widgets.Muted(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(width);
        int current = Array.FindIndex(UiSizes, s => s.Scale == _config.UiScale);
        int picked = Widgets.Combo("##uisize", current, UiSizes.Select(s => s.Label).ToArray());
        Widgets.Tip("How big the interface is. Auto follows your display. Ctrl + and Ctrl - also change it, and Ctrl 0 goes back to Auto.");
        if (picked != current) SetUiSize(UiSizes[picked].Scale);
        ImGui.SetCursorPos(after);
    }

    private void UiSizeShortcuts()
    {
        if (!ImGui.GetIO().KeyCtrl) return;

        float[] steps = UiSizes.Select(s => s.Scale).OfType<float>().ToArray();
        if (ImGui.IsKeyPressed(ImGuiKey.Equal, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadAdd, false))
        {
            float next = steps.FirstOrDefault(s => s > Theme.Scale + 0.01f);
            if (next > 0) SetUiSize(next);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Minus, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadSubtract, false))
        {
            float next = steps.LastOrDefault(s => s < Theme.Scale - 0.01f);
            if (next > 0) SetUiSize(next);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey._0, false) || ImGui.IsKeyPressed(ImGuiKey.Keypad0, false))
        {
            SetUiSize(null);
        }
    }

    private void SetUiSize(float? scale)
    {
        _config.UiScale = scale;
        _config.Save();
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

        int[] existing = _modVersions.Length > 0 ? _modVersions : _folderVersions;
        Widgets.Caption(existing.Length > 0
            ? $"{_generator.TableName(_version)}  ·  {(_modVersions.Length > 0 ? "The mod has" : "The mods have")} tables for {string.Join(", ", existing.Select(RestblGenerator.VersionName))}."
            : _generator.TableName(_version));
        ImGui.Dummy(new Vector2(0, Theme.Px(4)));
    }

    private void SelectGame(int game)
    {
        _game = game;
        _generator = null;
        _results = null;
        _result = null;
        _error = null;
        _view = [];
    }

    private void DefaultVersion()
    {
        if (_generator is null) return;
        int[] known = (_modVersions.Length > 0 ? _modVersions : _folderVersions).Where(_generator.Versions.Contains).ToArray();
        _version = known.Length == 1 ? known[0]
            : _generator.Versions.Contains(_version) ? _version
            : _config.Version is { } last && _generator.Versions.Contains(last) ? last
            : _generator.Latest;
    }

    private void Actions()
    {
        bool idle = _generator is not null && _run is null;

        ImGui.BeginDisabled(!idle || _modList.Length == 0);
        if (Widgets.PrimaryButton("Generate RESTBL")) Start(_modList, _mod, write: true);
        Widgets.Tip("Writes the table into the mod's System/Resource, replacing the one for this version if there is one.");
        ImGui.SameLine();
        if (ImGui.Button("Preview")) Start(_modList, _mod, write: false);
        Widgets.Tip("Works out the mod's entries without writing anything.");
        ImGui.EndDisabled();

        ImGui.SameLine(0, Theme.Px(24));
        ImGui.BeginDisabled(!idle || _folderList.Length == 0);
        if (Widgets.PrimaryButton(_folderList.Length > 0 ? $"Generate for {_folderList.Length} {(_folderList.Length == 1 ? "mod" : "mods")}###generateall" : "Generate for folder###generateall")) Start(_folderList, _folder, write: true);
        Widgets.Tip("Writes a table into each mod's System/Resource, replacing the one for this version if there is one.");
        ImGui.SameLine();
        if (ImGui.Button("Preview all")) Start(_folderList, _folder, write: false);
        Widgets.Tip("Works out every mod's entries without writing anything.");
        ImGui.EndDisabled();

        if (_run is null) return;
        ImGui.SameLine();
        (int done, int total) = _progress;
        string files = total == 0 ? "Listing files..." : $"{done:N0} / {total:N0} files";
        float fraction = total == 0 ? 0f : (float)done / total;
        ImGui.ProgressBar(_runMods.Length > 1 ? (_runMod + fraction) / _runMods.Length : fraction, new Vector2(-1, ImGui.GetFrameHeight()),
            _runMods.Length > 1 ? $"Mod {_runMod + 1} of {_runMods.Length}  ·  {files}" : files);
    }

    private void Results()
    {
        if (_error is not null) Widgets.Banner(Icon.Warning, _error, Theme.Error);

        if (_results is not { } results)
        {
            if (_error is null)
                Widgets.EmptyState("Nothing generated yet", "Pick a mod or a folder of mods, and the game version, then Generate. You can also drop folders onto the window.");
            return;
        }

        if (results.Count > 1)
        {
            BatchSummary(results);
            BatchList(results);
            ImGui.Dummy(new Vector2(0, Theme.Px(4)));
        }

        ModResult picked = results[_selected];
        if (picked.Error is { } failed) Widgets.Banner(Icon.Warning, failed, Theme.Error);
        else if (picked.Skipped is { } skipped) Widgets.Banner(Icon.Info, skipped, Theme.Warning);
        else if (_result is { } r) ModDetail(r);
    }

    private void BatchSummary(IReadOnlyList<ModResult> results)
    {
        int written = results.Count(r => r.Result?.Written is not null);
        int previewed = results.Count(r => r.Result is { Written: null, Changed: true });
        int none = results.Count(r => r.Result is { Changed: false });
        int skipped = results.Count(r => r.Skipped is not null);
        int failed = results.Count(r => r.Error is not null);

        List<string> parts = [];
        if (_runWrote) parts.Add($"Wrote {written} {(written == 1 ? "table" : "tables")}");
        else parts.Add($"Preview only, nothing written: {previewed} {(previewed == 1 ? "mod needs" : "mods need")} a table");
        if (none > 0) parts.Add($"{none} needed none");
        if (skipped > 0) parts.Add($"{skipped} skipped");
        if (failed > 0) parts.Add($"{failed} failed");

        Widgets.Banner(failed > 0 ? Icon.Warning : _runWrote ? Icon.Check : Icon.Info, string.Join(", ", parts) + $", from {results.Count} mods.",
            failed > 0 ? Theme.Warning : _runWrote ? Theme.Good : Theme.Accent);
    }

    private void BatchList(IReadOnlyList<ModResult> results)
    {
        const int visibleRows = 7;
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersOuter
                                      | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit;
        float height = (Math.Min(results.Count, visibleRows) + 1) * (ImGui.GetTextLineHeight() + ImGui.GetStyle().CellPadding.Y * 2 + 1) + Theme.Px(2);
        if (!ImGui.BeginTable("##batch", 6, flags, new Vector2(0, height))) return;

        float number = ImGui.CalcTextSize("000,000").X + Theme.Px(8);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Result", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Needs a table").X + Theme.Px(8));
        ImGui.TableSetupColumn("Added", ImGuiTableColumnFlags.WidthFixed, number);
        ImGui.TableSetupColumn("Grown", ImGuiTableColumnFlags.WidthFixed, number);
        ImGui.TableSetupColumn("Removed", ImGuiTableColumnFlags.WidthFixed, number);
        ImGui.TableSetupColumn("Kept", ImGuiTableColumnFlags.WidthFixed, number);
        ImGui.TableHeadersRow();

        for (int i = 0; i < results.Count; i++)
        {
            ModResult mod = results[i];
            ImGui.PushID(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (ImGui.Selectable(Path.GetRelativePath(_runRoot, mod.Mod), i == _selected, ImGuiSelectableFlags.SpanAllColumns)) SelectResult(i);
            ImGui.TableNextColumn();
            (string status, Vector4 colour) = mod.Error is not null ? ("Failed", Theme.Error)
                : mod.Skipped is not null ? ("Skipped", Theme.Warning)
                : mod.Result!.Written is not null ? ("Written", Theme.Good)
                : mod.Result.Changed ? ("Needs a table", Theme.Accent)
                : ("Not needed", Theme.TextMuted);
            ImGui.TextColored(colour, status);
            if (mod.Result is { } r)
            {
                int added = r.Entries.Count(e => e.Added), grown = r.Entries.Count(e => e.Grown), removed = r.Entries.Count(e => e.Removed);
                ImGui.TableNextColumn();
                Number((uint)added, Theme.TextMuted);
                ImGui.TableNextColumn();
                Number((uint)grown, Theme.TextMuted);
                ImGui.TableNextColumn();
                Number((uint)removed, Theme.TextMuted);
                ImGui.TableNextColumn();
                Number((uint)(r.Entries.Count - added - grown - removed), Theme.TextMuted);
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void SelectResult(int index)
    {
        _selected = index;
        _result = _results![index].Result;
        if (_filter == 4 && _result?.Entries.Any(e => e.Removed) != true) _filter = 0;
        RebuildView();
    }

    private void ModDetail(RestblResult r)
    {
        int added = r.Entries.Count(e => e.Added), grown = r.Entries.Count(e => e.Grown), removed = r.Entries.Count(e => e.Removed), kept = r.Entries.Count - added - grown - removed;
        string counts = $"{r.Modified:N0} of {r.Files:N0} files aren't vanilla {RestblGenerator.VersionName(r.Version)}: {added:N0} added, {grown:N0} grown, {(removed > 0 ? $"{removed:N0} removed, " : "")}{kept:N0} already big enough.";
        if (r.Written is { } written)
        {
            Widgets.Banner(Icon.Check, $"Wrote {Path.GetRelativePath(Path.GetDirectoryName(r.Romfs)!, written)}. {counts}", Theme.Good);
            if (ImGui.Button(OperatingSystem.IsWindows() ? "Show in Explorer" : "Show in folder")) Reveal(written);
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
        if (!ImGui.BeginTable("##entries", 4, flags, new Vector2(0, MathF.Max(ImGui.GetContentRegionAvail().Y, Theme.Px(200))))) return;

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
        if (_mod != _modDetected)
        {
            _modDetected = _mod;
            DetectFrom(_mod);
        }

        if (_folder != _folderDetected)
        {
            _folderDetected = _folder;
            DetectFrom(_folder);
        }
    }

    private void DetectFrom(string path)
    {
        try
        {
            if (Game.Detect(path) is { } id && Array.FindIndex(_games, g => g.Id == id) is var found and >= 0 && found != _game) SelectGame(found);
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

    private void CheckMods()
    {
        Game game = _games[_game];
        bool gameChanged = game.Id != _checkedGame;
        _checkedGame = game.Id;
        bool modChanged = gameChanged || _mod != _modChecked, folderChanged = gameChanged || _folder != _folderChecked;

        if (modChanged)
        {
            _modChecked = _mod;
            _modList = [];
            _modError = null;
            _modVersions = [];
            if (_mod.Length > 0)
            {
                try
                {
                    string main = game.FindRoots(_mod).Main;
                    _modList = [_mod];
                    _modWrites = Path.Combine(main, "System", "Resource");
                    _modVersions = RestblGenerator.ModVersions(main);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
                {
                    _modError = e.Message;
                }
            }
        }

        if (folderChanged)
        {
            _folderChecked = _folder;
            _folderList = [];
            _folderError = null;
            _folderVersions = [];
            if (_folder.Length > 0)
            {
                try
                {
                    _folderList = [.. game.FindMods(_folder)];
                    _folderVersions = _folderList.SelectMany(m => RestblGenerator.ModVersions(game.FindRoots(m).Main)).Distinct().Order().ToArray();
                }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
                {
                    _folderError = e.Message;
                }
            }
        }

        if (modChanged || folderChanged) DefaultVersion();
    }

    private void Start(string[] mods, string root, bool write)
    {
        _config.Game = _games[_game].Id;
        _config.Mod = _mod;
        _config.ModsFolder = _folder;
        _config.Version = _version;
        _config.Save();

        RestblGenerator generator = _generator!;
        int version = _version;
        _runMods = mods;
        _runRoot = root;
        _runMod = 0;
        _runWrote = write;
        _progress = default;
        _error = null;
        _run = Task.Run(() => generator.GenerateAll(mods, version, write, i =>
        {
            _runMod = i;
            _progress = default;
        }, new Progress(p => _progress = p)));
    }

    private void CollectRun()
    {
        if (_run is not { IsCompleted: true } run) return;
        _run = null;

        if (run.IsCompletedSuccessfully)
        {
            _results = run.Result;
            _modChecked = null;
            _folderChecked = null;
            SelectResult(0);
        }
        else
        {
            _results = null;
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
        else if (picking.IsFaulted) _error = $"Browse isn't available on this system, so paste the path instead. {picking.Exception!.InnerException!.Message}";
        _picked = null;
    }

    private void Reveal(string file)
    {
        try
        {
            NativeDialogs.Reveal(file);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _error = $"Couldn't open the folder: {e.Message}";
        }
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
