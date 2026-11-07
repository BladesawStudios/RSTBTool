using System.Numerics;
using ImGuiNET;

namespace RestblCalculator.Gui.UI;

public static class Theme
{
    public static readonly Vector4 Chrome = Hex(0x1B1B1C);
    public static readonly Vector4 Panel = Hex(0x252526);

    public static readonly Vector4 Raised = Hex(0x303032);

    public static readonly Vector4 Field = Hex(0x1A1A1B);
    public static readonly Vector4 FieldHover = Hex(0x202022);
    public static readonly Vector4 FieldActive = Hex(0x161617);

    public static readonly Vector4 Button = Hex(0x3A3A3C);
    public static readonly Vector4 ButtonHover = Hex(0x454548);
    public static readonly Vector4 ButtonActive = Hex(0x2E2E30);

    public static readonly Vector4 Line = Hex(0x111112);
    public static readonly Vector4 Edge = Hex(0x3E3E41);

    public static readonly Vector4 Text = Hex(0xD4D4D4);
    public static readonly Vector4 TextMuted = Hex(0x9B9B9B);
    public static readonly Vector4 TextFaint = Hex(0x6B6B6D);

    public static readonly Vector4 Accent = Hex(0x4A78B5);
    public static readonly Vector4 AccentHover = Hex(0x5886C3);
    public static readonly Vector4 AccentActive = Hex(0x3F6AA3);
    public static readonly Vector4 AccentSoft = Hex(0x4A78B5, 0.35f);
    public static readonly Vector4 OnAccent = Hex(0xF2F2F2);

    public static readonly Vector4 Warning = Hex(0xD9A441);
    public static readonly Vector4 Error = Hex(0xD9594F);
    public static readonly Vector4 Good = Hex(0x6FA84E);

    public static float Scale { get; private set; } = 1f;

    public static float Px(float px) => MathF.Round(px * Scale);

    public const float Round = 2f;

    public static void Apply(float scale)
    {
        Scale = scale;

        ImGui.StyleColorsDark();
        ImGuiStylePtr style = ImGui.GetStyle();

        style.WindowPadding = new Vector2(8, 8);
        style.FramePadding = new Vector2(6, 4);
        style.CellPadding = new Vector2(4, 2);
        style.ItemSpacing = new Vector2(6, 4);
        style.ItemInnerSpacing = new Vector2(4, 4);
        style.IndentSpacing = 14;
        style.ScrollbarSize = 12;
        style.GrabMinSize = 6;

        style.WindowRounding = 0;
        style.ChildRounding = 0;
        style.FrameRounding = Round;
        style.PopupRounding = 0;
        style.ScrollbarRounding = 0;
        style.GrabRounding = Round;
        style.TabRounding = 0;

        style.WindowBorderSize = 1;
        style.ChildBorderSize = 1;
        style.PopupBorderSize = 1;
        style.FrameBorderSize = 1;
        style.TabBorderSize = 0;

        style.WindowTitleAlign = new Vector2(0f, 0.5f);
        style.WindowMenuButtonPosition = ImGuiDir.None;
        style.SelectableTextAlign = new Vector2(0f, 0.5f);
        style.SeparatorTextBorderSize = 1;
        style.DisabledAlpha = 0.45f;

        RangeAccessor<Vector4> c = style.Colors;
        c[(int)ImGuiCol.Text] = Text;
        c[(int)ImGuiCol.TextDisabled] = TextFaint;
        c[(int)ImGuiCol.WindowBg] = Panel;
        c[(int)ImGuiCol.ChildBg] = Vector4.Zero;
        c[(int)ImGuiCol.PopupBg] = Raised;
        c[(int)ImGuiCol.Border] = Line;
        c[(int)ImGuiCol.BorderShadow] = Vector4.Zero;

        c[(int)ImGuiCol.FrameBg] = Field;
        c[(int)ImGuiCol.FrameBgHovered] = FieldHover;
        c[(int)ImGuiCol.FrameBgActive] = FieldActive;

        c[(int)ImGuiCol.TitleBg] = Chrome;
        c[(int)ImGuiCol.TitleBgActive] = Chrome;
        c[(int)ImGuiCol.TitleBgCollapsed] = Chrome;
        c[(int)ImGuiCol.MenuBarBg] = Chrome;

        c[(int)ImGuiCol.ScrollbarBg] = Hex(0x000000, 0.12f);
        c[(int)ImGuiCol.ScrollbarGrab] = Hex(0x48484B);
        c[(int)ImGuiCol.ScrollbarGrabHovered] = Hex(0x56565A);
        c[(int)ImGuiCol.ScrollbarGrabActive] = Hex(0x636367);

        c[(int)ImGuiCol.CheckMark] = OnAccent;
        c[(int)ImGuiCol.SliderGrab] = Hex(0x6A6A6E);
        c[(int)ImGuiCol.SliderGrabActive] = Accent;

        c[(int)ImGuiCol.Button] = Button;
        c[(int)ImGuiCol.ButtonHovered] = ButtonHover;
        c[(int)ImGuiCol.ButtonActive] = ButtonActive;

        c[(int)ImGuiCol.Header] = AccentSoft;
        c[(int)ImGuiCol.HeaderHovered] = Hex(0xFFFFFF, 0.05f);
        c[(int)ImGuiCol.HeaderActive] = Hex(0x4A78B5, 0.5f);

        c[(int)ImGuiCol.Separator] = Line;
        c[(int)ImGuiCol.SeparatorHovered] = Accent;
        c[(int)ImGuiCol.SeparatorActive] = Accent;

        c[(int)ImGuiCol.ResizeGrip] = Vector4.Zero;
        c[(int)ImGuiCol.ResizeGripHovered] = AccentSoft;
        c[(int)ImGuiCol.ResizeGripActive] = Accent;

        c[(int)ImGuiCol.Tab] = Chrome;
        c[(int)ImGuiCol.TabHovered] = Raised;
        c[(int)ImGuiCol.TabActive] = Panel;
        c[(int)ImGuiCol.TabUnfocused] = Chrome;
        c[(int)ImGuiCol.TabUnfocusedActive] = Panel;

        c[(int)ImGuiCol.TableHeaderBg] = Raised;
        c[(int)ImGuiCol.TableBorderStrong] = Line;
        c[(int)ImGuiCol.TableBorderLight] = Line;
        c[(int)ImGuiCol.TableRowBg] = Vector4.Zero;
        c[(int)ImGuiCol.TableRowBgAlt] = Hex(0xFFFFFF, 0.02f);

        c[(int)ImGuiCol.TextSelectedBg] = Hex(0x4A78B5, 0.45f);
        c[(int)ImGuiCol.DragDropTarget] = Accent;
        c[(int)ImGuiCol.NavHighlight] = Accent;
        c[(int)ImGuiCol.PlotHistogram] = Accent;
        c[(int)ImGuiCol.ModalWindowDimBg] = Hex(0x000000, 0.45f);

        style.ScaleAllSizes(scale);
    }

    public static uint U32(Vector4 colour) => ImGui.GetColorU32(colour);

    public static Vector4 Fade(Vector4 colour, float alpha) => colour with { W = alpha };

    private static Vector4 Hex(uint rgb, float alpha = 1f) => new(
        ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
}
