using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace RestblCalculator.Gui.UI;

public static unsafe class Fonts
{
    public static ImFontPtr Body;
    public static ImFontPtr Bold;
    public static ImFontPtr Small;
    public static ImFontPtr Title;
    public static ImFontPtr Mono;

    public const float BodySize = 14f;
    public const float SmallSize = 12f;
    public const float TitleSize = 18f;
    public const float MonoSize = 13f;

    private static GCHandle _textRanges;
    private static GCHandle _iconRanges;

    private static readonly string WindowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    public static readonly bool WindowsFaces = OperatingSystem.IsWindows()
        && Existing(WindowsDir, "tahoma.ttf", "segoeui.ttf") is not null
        && Existing(WindowsDir, "SegoeIcons.ttf", "segmdl2.ttf") is not null;

    public static void Load(float scale)
    {
        ImFontAtlasPtr atlas = ImGui.GetIO().Fonts;
        string dir = WindowsFaces ? WindowsDir : Path.Combine(AppContext.BaseDirectory, "Fonts");

        string? regular = Existing(dir, "tahoma.ttf", "segoeui.ttf", "DejaVuSans.ttf");
        string? bold = Existing(dir, "tahomabd.ttf", "seguisb.ttf", "DejaVuSans-Bold.ttf") ?? regular;
        string? mono = Existing(dir, "CascadiaMono.ttf", "consola.ttf", "DejaVuSansMono.ttf") ?? regular;
        string? icons = Existing(dir, "SegoeIcons.ttf", "segmdl2.ttf", "fa-solid-900.ttf");
        string? japanese = Existing(dir, "YuGothM.ttc", "meiryo.ttc", "msgothic.ttc");

        if (regular is null)
        {
            Body = Bold = Small = Title = Mono = atlas.AddFontDefault();
            return;
        }

        _textRanges = GCHandle.Alloc(new ushort[]
        {
            0x0020, 0x017F, 0x0400, 0x04FF, 0x2010, 0x2027, 0x2190, 0x2193, 0x2212, 0x2212, 0x2248, 0x2248, 0,
        }, GCHandleType.Pinned);

        ushort[] iconRanges = [.. Icon.All.Select(i => (ushort)i[0]).Distinct().Order().SelectMany(c => new[] { c, c }), 0];
        _iconRanges = GCHandle.Alloc(iconRanges, GCHandleType.Pinned);

        Body = Add(atlas, regular, BodySize * scale, icons, japanese);
        Bold = Add(atlas, bold!, BodySize * scale, icons, null);
        Small = Add(atlas, regular, SmallSize * scale, icons, null);
        Title = Add(atlas, bold!, TitleSize * scale, icons, null);
        Mono = Add(atlas, mono!, MonoSize * scale, null, null);
    }

    private static ImFontPtr Add(ImFontAtlasPtr atlas, string path, float size, string? icons, string? japanese)
    {
        ImFontConfigPtr cfg = ImGuiNative.ImFontConfig_ImFontConfig();
        cfg.OversampleH = 2;
        cfg.OversampleV = 2;
        ImFontPtr font = atlas.AddFontFromFileTTF(path, size, cfg, _textRanges.AddrOfPinnedObject());
        cfg.Destroy();

        if (japanese is not null)
        {
            ImFontConfigPtr jp = ImGuiNative.ImFontConfig_ImFontConfig();
            jp.MergeMode = true;
            jp.PixelSnapH = true;
            jp.OversampleH = 1;
            atlas.AddFontFromFileTTF(japanese, size, jp, atlas.GetGlyphRangesJapanese());
            jp.Destroy();
        }

        if (icons is null) return font;

        ImFontConfigPtr merge = ImGuiNative.ImFontConfig_ImFontConfig();
        merge.MergeMode = true;
        merge.PixelSnapH = true;
        merge.GlyphMinAdvanceX = size * 1.05f;
        merge.GlyphOffset = new Vector2(0f, MathF.Round(size * 0.14f));
        atlas.AddFontFromFileTTF(icons, size * 0.95f, merge, _iconRanges.AddrOfPinnedObject());
        merge.Destroy();

        return font;
    }

    private static string? Existing(string dir, params string[] names)
        => names.Select(n => Path.Combine(dir, n)).FirstOrDefault(File.Exists);
}
