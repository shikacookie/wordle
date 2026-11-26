using Godot;

namespace WordleResearch.UI;

internal static class CreamTheme
{
    public static readonly Color Background = new("FBF3F5");
    public static readonly Color Cocoa = new("5C4952");
    public static readonly Color Panel = new("F8E8EE");
    public static readonly Color Border = new("E8CED8");
    public static readonly Color Ink = new("4A3B42");
    public static readonly Color Muted = new("775864");
    public static readonly Color Sakura = new("FCEEF2");
    public static readonly Color ButtonBorder = new("E5C1CD");
    public static readonly Color Peach = new("F2A7B5");
    public static readonly Color Coral = new("C55372");
    public static readonly Color Mint = new("6BBF8A");
    public static readonly Color Gold = new("E5B85C");
    public static readonly Color Gray = new("B8A9AF");

    public static StyleBoxFlat Box(Color fill, Color border, int width = 1, int horizontal = 12, int vertical = 6)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fill, BorderColor = border,
            ContentMarginLeft = horizontal, ContentMarginRight = horizontal,
            ContentMarginTop = vertical, ContentMarginBottom = vertical
        };
        style.SetCornerRadiusAll(8);
        style.SetBorderWidthAll(width);
        return style;
    }

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 16, DefaultFont = LoadRoundedFont() };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "MutedLabel", Muted);
        theme.SetTypeVariation("MutedLabel", "Label");
        theme.SetStylebox("panel", "PanelContainer", Box(Panel, Border, 1, 16, 14));
        theme.SetTypeVariation("LetterTag", "PanelContainer");
        theme.SetStylebox("panel", "LetterTag", Box(Panel, Cocoa, 1, 6, 3));
        theme.SetTypeVariation("LetterTagLabel", "Label");
        theme.SetFont("font", "LetterTagLabel", new FontVariation
        {
            BaseFont = theme.DefaultFont, VariationEmbolden = 0.65f
        });
        theme.SetFontSize("font_size", "LetterTagLabel", 16);
        theme.SetTypeVariation("SidebarPanel", "PanelContainer");
        theme.SetStylebox("panel", "SidebarPanel", new StyleBoxEmpty
        {
            ContentMarginLeft = 8, ContentMarginRight = 4, ContentMarginTop = 12, ContentMarginBottom = 12
        });
        theme.SetTypeVariation("SidebarDivider", "HSeparator");
        theme.SetStylebox("separator", "SidebarDivider", Box(Cocoa, Cocoa, 0, 0, 0));
        theme.SetConstant("separation", "SidebarDivider", 1);
        ButtonStyles(theme, "Button", Sakura, new Color("F9E2E9"), new Color("F5D9E2"), ButtonBorder);
        theme.SetTypeVariation("PrimaryButton", "Button");
        ButtonStyles(theme, "PrimaryButton", Peach, new Color("EE98A9"), new Color("E98E9F"), ButtonBorder);

        foreach (string state in new[] { "font_color", "font_hover_color", "font_focus_color",
                     "font_pressed_color", "font_hover_pressed_color" })
            theme.SetColor(state, "CheckButton", Ink);
        theme.SetIcon("checked", "CheckButton", ToggleIcon(true));
        theme.SetIcon("unchecked", "CheckButton", ToggleIcon(false));
        theme.SetStylebox("focus", "CheckButton", FocusRing());
        theme.SetConstant("separation", "HBoxContainer", 8);
        theme.SetConstant("separation", "VBoxContainer", 8);
        theme.SetConstant("h_separation", "GridContainer", 6);
        theme.SetConstant("v_separation", "GridContainer", 6);
        theme.SetStylebox("separator", "HSeparator", Box(Border, Border, 0, 0, 1));
        theme.SetStylebox("scroll", "VScrollBar", Box(new Color("F1DCE4"), Border, 0, 4, 1));
        theme.SetStylebox("scroll_focus", "VScrollBar", Box(new Color("F1DCE4"), Border, 0, 4, 1));
        theme.SetStylebox("grabber", "VScrollBar", Box(new Color("D9A6B7"), Border, 0, 4, 3));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Box(Peach, Border, 0, 4, 3));
        theme.SetStylebox("grabber_pressed", "VScrollBar", Box(Coral, Border, 0, 4, 3));
        return theme;
    }

    private static Font LoadRoundedFont()
    {
        var system = new SystemFont
        {
            FontNames = new[] { "Fredoka", "Arial Rounded MT Bold", "Microsoft JhengHei UI", "Yu Gothic UI", "sans-serif" }
        };
        foreach (string path in new[] { "res://Fredoka.ttf", "res://Fredoka-SemiBold.ttf" })
        {
            Font font = ResourceLoader.Exists(path) ? ResourceLoader.Load<Font>(path) : null;
            if (font == null && FileAccess.FileExists(path))
            {
                var source = new FontFile();
                if (source.LoadDynamicFont(path) == Error.Ok)
                    font = source;
                else
                    source.Dispose();
            }
            if (font != null)
                return new FontVariation { BaseFont = font, Fallbacks = new Godot.Collections.Array<Font> { system } };
        }
        return system;
    }

    public static StyleBoxFlat Keycap(Color fill)
    {
        var style = Box(fill, Cocoa, 2, 0, 0);
        style.ShadowColor = Cocoa;
        style.ShadowSize = 0;
        style.ShadowOffset = new Vector2(3, 3);
        return style;
    }

    // Godot does not render a zero-size StyleBox shadow: draw a second hard silhouette instead.
    public static Godot.Panel AddKeycapLayer(Control parent, StyleBoxFlat style, int offset)
    {
        var layer = new Godot.Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(layer);
        layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        OffsetLayer(layer, offset);
        return layer;
    }

    public static void OffsetLayer(Control layer, int offset)
    {
        layer.OffsetLeft = layer.OffsetTop = layer.OffsetRight = layer.OffsetBottom = offset;
    }

    private static ImageTexture ToggleIcon(bool enabled)
    {
        using var image = new Image();
        string fill = enabled ? "#F5D9E2" : "#FCEEF2";
        int handle = enabled ? 25 : 11;
        image.LoadSvgFromString(
            $"<svg xmlns='http://www.w3.org/2000/svg' width='36' height='22' viewBox='0 0 36 22'>" +
            $"<rect x='1' y='2' width='34' height='18' rx='8' fill='{fill}' stroke='#775864'/>" +
            $"<circle cx='{handle}' cy='11' r='5.5' fill='#4A3B42'/></svg>");
        return ImageTexture.CreateFromImage(image);
    }

    private static StyleBoxFlat FocusRing() => Box(Colors.Transparent, Coral, 3, 0, 0);

    private static void ButtonStyles(Theme theme, string type, Color fill, Color hover, Color pressed, Color border,
        int verticalPadding = 6)
    {
        theme.SetStylebox("normal", type, Box(fill, border, vertical: verticalPadding));
        theme.SetStylebox("hover", type, Box(hover, border, vertical: verticalPadding));
        theme.SetStylebox("pressed", type, Box(pressed, border, vertical: verticalPadding));
        theme.SetStylebox("hover_pressed", type, Box(pressed, border, vertical: verticalPadding));
        theme.SetStylebox("disabled", type, Box(new Color("F2DDE5"), Border, vertical: verticalPadding));
        theme.SetStylebox("focus", type, FocusRing());
        foreach (string state in new[] { "font_color", "font_hover_color", "font_focus_color",
                     "font_pressed_color", "font_hover_pressed_color", "font_disabled_color" })
            theme.SetColor(state, type, Ink);
    }
}
