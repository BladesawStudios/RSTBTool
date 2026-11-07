using System.Numerics;
using ImGuiNET;

namespace RestblCalculator.Gui.UI;

public static class Widgets
{
    public static void Tip(string text)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort | ImGuiHoveredFlags.AllowWhenDisabled)) return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(Theme.Px(340));
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public static void Muted(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    public static void MutedWrapped(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    public static void Caption(string text, Vector4? colour = null)
    {
        ImGui.PushFont(Fonts.Small);
        ImGui.PushStyleColor(ImGuiCol.Text, colour ?? Theme.TextMuted);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        ImGui.PopFont();
    }

    public static void Bold(string text)
    {
        ImGui.PushFont(Fonts.Bold);
        ImGui.TextUnformatted(text);
        ImGui.PopFont();
    }

    public static void Mono(string text, Vector4? colour = null)
    {
        ImGui.PushFont(Fonts.Mono);
        ImGui.PushStyleColor(ImGuiCol.Text, colour ?? Theme.Text);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        ImGui.PopFont();
    }

    public static void Ellipsis(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width)
        {
            ImGui.TextUnformatted(text);
            return;
        }

        const string dots = "...";
        int keep = text.Length;
        while (keep > 1 && ImGui.CalcTextSize(text[..keep] + dots).X > width) keep--;

        ImGui.TextUnformatted(text[..keep] + dots);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    public static void Note(string text, Vector4 colour)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, colour);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    public static void Banner(string icon, string text, Vector4 colour)
    {
        Vector2 pos = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float pad = Theme.Px(8);

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(pos + new Vector2(pad, pad));
        ImGui.PushStyleColor(ImGuiCol.Text, colour);
        ImGui.TextUnformatted(icon);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushTextWrapPos(pos.X - ImGui.GetWindowPos().X + ImGui.GetScrollX() + width - pad);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        float bottom = ImGui.GetItemRectMax().Y + pad;

        draw.ChannelsSetCurrent(0);
        draw.AddRectFilled(pos, new Vector2(pos.X + width, bottom), Theme.U32(Theme.Fade(colour, 0.10f)), Theme.Round);
        draw.AddRectFilled(pos, new Vector2(pos.X + Theme.Px(3), bottom), Theme.U32(colour), Theme.Round);
        draw.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(pos.X, bottom));
        ImGui.Dummy(new Vector2(width, Theme.Px(4)));
    }

    public static void Chip(string text, Vector4 colour)
    {
        ImGui.PushFont(Fonts.Small);
        Vector2 size = ImGui.CalcTextSize(text);
        Vector2 pad = new(Theme.Px(6), Theme.Px(1));
        Vector2 pos = ImGui.GetCursorScreenPos();
        float frame = ImGui.GetFrameHeight();
        pos.Y += MathF.Round((frame - size.Y - pad.Y * 2) * 0.5f);

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(pos, pos + size + pad * 2, Theme.U32(Theme.Fade(colour, 0.18f)), Theme.Px(8));
        draw.AddText(pos + pad, Theme.U32(colour), text);
        ImGui.Dummy(new Vector2(size.X + pad.X * 2, frame));
        ImGui.PopFont();
    }

    public static void EmptyState(string title, string hint)
    {
        Vector2 avail = ImGui.GetContentRegionAvail();
        float wrap = MathF.Min(avail.X - Theme.Px(40), Theme.Px(420));
        ImGui.PushFont(Fonts.Title);
        Vector2 titleSize = ImGui.CalcTextSize(title);
        ImGui.PopFont();
        Vector2 hintSize = ImGui.CalcTextSize(hint, wrap);

        Vector2 start = ImGui.GetCursorPos();
        float top = start.Y + MathF.Max(0, (avail.Y - titleSize.Y - hintSize.Y - Theme.Px(8)) * 0.42f);

        ImGui.SetCursorPos(new Vector2(start.X + (avail.X - titleSize.X) * 0.5f, top));
        ImGui.PushFont(Fonts.Title);
        ImGui.TextColored(Theme.TextMuted, title);
        ImGui.PopFont();

        ImGui.SetCursorPos(new Vector2(start.X + (avail.X - hintSize.X) * 0.5f, top + titleSize.Y + Theme.Px(8)));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
        ImGui.TextColored(Theme.TextFaint, hint);
        ImGui.PopTextWrapPos();
    }

    public static void Key(string key)
    {
        ImGui.PushFont(Fonts.Small);
        Vector2 size = ImGui.CalcTextSize(key);
        Vector2 pad = new(Theme.Px(5), Theme.Px(1));
        Vector2 pos = ImGui.GetCursorScreenPos();

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(pos, pos + size + pad * 2, Theme.U32(Theme.Button), Theme.Round);
        draw.AddRect(pos, pos + size + pad * 2, Theme.U32(Theme.Line), Theme.Round);
        draw.AddText(pos + pad, Theme.U32(Theme.Text), key);
        ImGui.Dummy(size + pad * 2);
        ImGui.PopFont();
    }

    public static bool Section(string title, bool defaultOpen = true, string? trailing = null)
    {
        ImGuiStoragePtr storage = ImGui.GetStateStorage();
        uint id = ImGui.GetID($"section:{title}");
        bool open = storage.GetBool(id, defaultOpen);

        float pad = ImGui.GetStyle().WindowPadding.X;
        Vector2 pos = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float height = ImGui.GetFrameHeight();

        ImGui.Dummy(new Vector2(0, Theme.Px(2)));
        pos.Y += Theme.Px(2);
        ImGui.SetCursorScreenPos(pos);

        if (ImGui.InvisibleButton($"##section:{title}", new Vector2(width, height)))
        {
            open = !open;
            storage.SetBool(id, open);
        }

        bool hovered = ImGui.IsItemHovered();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();

        Vector2 min = new(pos.X - pad, pos.Y);
        Vector2 max = new(pos.X + width + pad, pos.Y + height);
        PushBarClip(draw, min, max);
        draw.AddRectFilled(min, max, Theme.U32(hovered ? Theme.Button : Theme.Raised));
        draw.AddLine(min, new Vector2(max.X, min.Y), Theme.U32(Theme.Edge));
        draw.AddLine(new Vector2(min.X, max.Y - 1), new Vector2(max.X, max.Y - 1), Theme.U32(Theme.Line));
        draw.PopClipRect();

        float mid = pos.Y + height * 0.5f;
        Arrow(draw, new Vector2(pos.X + Theme.Px(5), mid), open, Theme.TextMuted);
        draw.AddText(new Vector2(pos.X + Theme.Px(16), mid - ImGui.GetTextLineHeight() * 0.5f), Theme.U32(Theme.Text), title);

        if (trailing is not null)
        {
            ImGui.PushFont(Fonts.Small);
            Vector2 size = ImGui.CalcTextSize(trailing);
            draw.AddText(new Vector2(pos.X + width - size.X - Theme.Px(4), mid - size.Y * 0.5f), Theme.U32(Theme.TextMuted), trailing);
            ImGui.PopFont();
        }

        ImGui.Dummy(new Vector2(0, Theme.Px(1)));
        return open;
    }

    private static void PushBarClip(ImDrawListPtr draw, Vector2 min, Vector2 max)
    {
        Vector2 win = ImGui.GetWindowPos();
        Vector2 size = ImGui.GetWindowSize();
        float right = win.X + ImGui.GetWindowContentRegionMax().X + ImGui.GetStyle().WindowPadding.X;

        draw.PushClipRect(
            new Vector2(MathF.Max(min.X, win.X), MathF.Max(min.Y, win.Y)),
            new Vector2(MathF.Min(max.X, right), MathF.Min(max.Y, win.Y + size.Y)),
            false);
    }

    private static void Arrow(ImDrawListPtr draw, Vector2 centre, bool down, Vector4 colour)
    {
        float s = Theme.Px(3.5f);
        if (down)
            draw.AddTriangleFilled(centre + new Vector2(-s, -s * 0.5f), centre + new Vector2(s, -s * 0.5f),
                centre + new Vector2(0, s * 0.6f), Theme.U32(colour));
        else
            draw.AddTriangleFilled(centre + new Vector2(-s * 0.5f, -s), centre + new Vector2(s * 0.6f, 0),
                centre + new Vector2(-s * 0.5f, s), Theme.U32(colour));
    }

    public static int TabStrip(string id, int current, string[] tabs, string?[]? badges = null)
    {
        ImGui.PushID(id);

        float pad = ImGui.GetStyle().WindowPadding.X;
        Vector2 origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float height = ImGui.GetFrameHeight() + Theme.Px(4);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();

        Vector2 stripMin = new(origin.X - pad, origin.Y);
        Vector2 stripMax = new(origin.X + width + pad, origin.Y + height);
        PushBarClip(draw, stripMin, stripMax);
        draw.AddRectFilled(stripMin, stripMax, Theme.U32(Theme.Chrome));
        draw.AddLine(new Vector2(stripMin.X, stripMax.Y - 1), new Vector2(stripMax.X, stripMax.Y - 1), Theme.U32(Theme.Line));
        draw.PopClipRect();

        float x = origin.X;
        for (int i = 0; i < tabs.Length; i++)
        {
            string? badge = badges is not null && i < badges.Length ? badges[i] : null;
            Vector2 text = ImGui.CalcTextSize(tabs[i]);
            ImGui.PushFont(Fonts.Small);
            float badgeWidth = badge is null ? 0 : ImGui.CalcTextSize(badge).X + Theme.Px(6);
            ImGui.PopFont();
            float w = text.X + badgeWidth + Theme.Px(24);
            Vector2 min = new(x, origin.Y);

            ImGui.SetCursorScreenPos(min);
            if (ImGui.InvisibleButton($"tab{i}", new Vector2(w, height))) current = i;

            bool active = i == current;
            bool hovered = ImGui.IsItemHovered();

            if (active)
            {
                draw.AddRectFilled(min, min + new Vector2(w, height), Theme.U32(Theme.Panel));
                draw.AddLine(min, min + new Vector2(0, height), Theme.U32(Theme.Line));
                draw.AddLine(min + new Vector2(w, 0), min + new Vector2(w, height), Theme.U32(Theme.Line));
                draw.AddLine(min, min + new Vector2(w, 0), Theme.U32(Theme.Accent), Theme.Px(2));
            }

            Vector2 at = min + new Vector2(Theme.Px(12), (height - text.Y) * 0.5f);
            draw.AddText(at, Theme.U32(active || hovered ? Theme.Text : Theme.TextMuted), tabs[i]);
            if (badge is not null)
            {
                ImGui.PushFont(Fonts.Small);
                Vector2 size = ImGui.CalcTextSize(badge);
                draw.AddText(new Vector2(at.X + text.X + Theme.Px(6), min.Y + (height - size.Y) * 0.5f), Theme.U32(Theme.TextFaint), badge);
                ImGui.PopFont();
            }

            x += w;
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0, height));
        ImGui.Dummy(new Vector2(width, Theme.Px(4)));

        ImGui.PopID();
        return current;
    }

    public static int Segmented(string id, int current, string[] items, float width = -1f, string[]? tips = null)
    {
        ImGui.PushID(id);

        if (width <= 0) width = ImGui.GetContentRegionAvail().X;
        float height = ImGui.GetFrameHeight();
        float each = MathF.Floor(width / items.Length);
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();

        draw.AddRectFilled(origin, origin + new Vector2(width, height), Theme.U32(Theme.Button), Theme.Round);

        for (int i = 0; i < items.Length; i++)
        {
            Vector2 min = origin + new Vector2(each * i, 0);
            float w = i == items.Length - 1 ? width - each * i : each;

            ImGui.SetCursorScreenPos(min);
            if (ImGui.InvisibleButton($"seg{i}", new Vector2(w, height))) current = i;
            if (tips is not null && i < tips.Length) Tip(tips[i]);

            bool active = i == current;
            bool hovered = ImGui.IsItemHovered();

            ImDrawFlags corners = items.Length == 1 ? ImDrawFlags.RoundCornersAll
                : i == 0 ? ImDrawFlags.RoundCornersLeft
                : i == items.Length - 1 ? ImDrawFlags.RoundCornersRight
                : ImDrawFlags.RoundCornersNone;

            if (active || hovered)
                draw.AddRectFilled(min, min + new Vector2(w, height), Theme.U32(active ? Theme.Accent : Theme.ButtonHover), Theme.Round, corners);

            if (i > 0 && !active && i - 1 != current)
                draw.AddLine(min + new Vector2(0, 3), min + new Vector2(0, height - 3), Theme.U32(Theme.Line));

            Vector2 size = ImGui.CalcTextSize(items[i]);
            Vector2 textPos = min + new Vector2(MathF.Max(Theme.Px(4), (w - size.X) * 0.5f), (height - size.Y) * 0.5f);
            draw.PushClipRect(min, min + new Vector2(w, height), true);
            draw.AddText(textPos, Theme.U32(active ? Theme.OnAccent : Theme.Text), items[i]);
            draw.PopClipRect();
        }

        draw.AddRect(origin, origin + new Vector2(width, height), Theme.U32(Theme.Line), Theme.Round);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));

        ImGui.PopID();
        return current;
    }

    public static bool Check(string label, bool value)
    {
        bool on = value;
        if (on)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Accent);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Theme.AccentHover);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Theme.AccentActive);
        }

        ImGui.Checkbox(label, ref value);

        if (on) ImGui.PopStyleColor(3);
        return value;
    }

    public static bool BeginCombo(string id, string preview)
    {
        Vector2 pos = ImGui.GetCursorScreenPos();
        float width = ImGui.CalcItemWidth();
        float height = ImGui.GetFrameHeight();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();

        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Button);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Theme.ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Theme.ButtonActive);
        bool open = ImGui.BeginCombo(id, preview, ImGuiComboFlags.NoArrowButton);
        ImGui.PopStyleColor(3);

        Arrow(draw, new Vector2(pos.X + width - Theme.Px(10), pos.Y + height * 0.5f), true, Theme.TextMuted);
        return open;
    }

    public static int Combo(string id, int current, string[] items)
    {
        string preview = current >= 0 && current < items.Length ? items[current] : "";
        if (!BeginCombo(id, preview)) return current;

        for (int i = 0; i < items.Length; i++)
        {
            if (ImGui.Selectable(items[i], i == current)) current = i;
            if (i == current) ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
        return current;
    }

    public static bool IconButton(string icon, string tooltip, bool active = false, float size = 0f, Vector4? colour = null)
    {
        float s = size > 0 ? size : ImGui.GetFrameHeight();
        Vector2 pos = ImGui.GetCursorScreenPos();

        bool pressed = ImGui.InvisibleButton($"{icon}##{tooltip}", new Vector2(s, s));
        bool hovered = ImGui.IsItemHovered();
        bool held = ImGui.IsItemActive();
        Tip(tooltip);

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector4? bg = active ? (hovered ? Theme.AccentHover : Theme.Accent)
                    : held ? Theme.ButtonActive
                    : hovered ? Theme.ButtonHover
                    : null;

        if (bg is { } b) draw.AddRectFilled(pos, pos + new Vector2(s, s), Theme.U32(b), Theme.Round);

        Vector4 fg = active ? Theme.OnAccent : hovered ? colour ?? Theme.Text : colour ?? Theme.TextMuted;
        Glyph(draw, icon, pos + new Vector2(s, s) * 0.5f, fg);

        return pressed;
    }

    private static unsafe void Glyph(ImDrawListPtr draw, string icon, Vector2 centre, Vector4 colour)
    {
        ImFontPtr font = ImGui.GetFont();
        ImFontGlyphPtr g = font.FindGlyph(icon[0]);
        if (g.NativePtr is null)
        {
            draw.AddText(centre - ImGui.CalcTextSize(icon) * 0.5f, Theme.U32(colour), icon);
            return;
        }

        float scale = ImGui.GetFontSize() / font.FontSize;
        Vector2 ink = new((g.X0 + g.X1) * 0.5f, (g.Y0 + g.Y1) * 0.5f);
        Vector2 at = centre - ink * scale;
        draw.AddText(new Vector2(MathF.Round(at.X), MathF.Round(at.Y)), Theme.U32(colour), icon);
    }

    public static bool PrimaryButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentActive);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.OnAccent);
        bool pressed = ImGui.Button(label, size);
        ImGui.PopStyleColor(4);
        return pressed;
    }

    public static bool GhostButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        bool pressed = ImGui.Button(label, size);
        ImGui.PopStyleColor(3);
        return pressed;
    }

    public static bool Search(string id, ref string text, string hint, uint maxLength = 128, bool focus = false)
    {
        float frame = ImGui.GetFrameHeight();
        Vector2 pos = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;

        Vector2 pad = ImGui.GetStyle().FramePadding;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(frame - Theme.Px(2), pad.Y));
        ImGui.SetNextItemWidth(width);
        ImGui.SetNextItemAllowOverlap();
        if (focus) ImGui.SetKeyboardFocusHere();
        bool changed = ImGui.InputTextWithHint(id, hint, ref text, maxLength);
        ImGui.PopStyleVar();

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        ImGui.PushFont(Fonts.Small);
        Glyph(draw, Icon.Search, pos + new Vector2(frame, frame) * 0.5f, Theme.TextFaint);
        ImGui.PopFont();

        if (text.Length == 0) return changed;

        Vector2 after = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(pos + new Vector2(width - frame, 0));
        ImGui.PushFont(Fonts.Small);
        if (IconButton(Icon.Close, "Clear", false, frame))
        {
            text = "";
            changed = true;
        }
        ImGui.PopFont();
        ImGui.SetCursorScreenPos(after);
        return changed;
    }

    public static bool BeginList(string id, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.Field);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Theme.Px(2), Theme.Px(2)));
        bool open = ImGui.BeginChild(id, size, ImGuiChildFlags.Border | ImGuiChildFlags.AlwaysUseWindowPadding);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        return open;
    }

    public const ImGuiWindowFlags FixedPanel =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;

    public static bool BeginDialog(string id, float width)
    {
        if (!ImGui.IsPopupOpen(id)) ImGui.OpenPopup(id);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        ImGui.SetNextWindowSize(new Vector2(Theme.Px(width), 0), ImGuiCond.Appearing);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Theme.Px(16), Theme.Px(14)));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Theme.Panel);
        bool open = ImGui.BeginPopupModal(id, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        return open;
    }

    public static void DialogTitle(string title, string? subtitle = null)
    {
        ImGui.PushFont(Fonts.Title);
        ImGui.TextUnformatted(title);
        ImGui.PopFont();
        if (subtitle is not null) MutedWrapped(subtitle);
        ImGui.Dummy(new Vector2(0, Theme.Px(6)));
    }

    public static void AlignRight(float width)
    {
        float avail = ImGui.GetContentRegionAvail().X;
        if (avail > width) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - width);
    }

    public static float ButtonWidth(params string[] labels)
        => labels.Sum(l => ImGui.CalcTextSize(l).X + ImGui.GetStyle().FramePadding.X * 2)
           + ImGui.GetStyle().ItemSpacing.X * (labels.Length - 1);
}
