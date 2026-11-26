using Godot;

namespace WordleResearch.UI;

internal partial class TactileButton : Button
{
    private Panel _face;
    private Label _caption;
    private Color? _captionColor;
    private bool _pointerDown;
    private bool _physicalDown;
    private bool _hover;
    private Color _fill;
    private Color _hoverFill;
    private StyleBoxFlat _normal;
    private StyleBoxFlat _over;

    public override void _Ready()
    {
        if (_normal == null)
            SetPalette(CreamTheme.Sakura, CreamTheme.Sakura.Darkened(0.06f));
        var empty = new StyleBoxEmpty();
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            AddThemeStyleboxOverride(state, empty);
        foreach (string key in new[] { "font_color", "font_hover_color", "font_pressed_color",
                     "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
            AddThemeColorOverride(key, Colors.Transparent);
        CreamTheme.AddKeycapLayer(this, CreamTheme.Keycap(CreamTheme.Cocoa), 3);
        _face = CreamTheme.AddKeycapLayer(this, CreamTheme.Keycap(_fill), 0);
        _caption = new Label { Text = Text, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _face.AddChild(_caption);
        _caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetCaptionColor(_captionColor);
        ButtonDown += () => { _pointerDown = true; Refresh(); };
        ButtonUp += () => { _pointerDown = false; Refresh(); };
        MouseEntered += () => { _hover = true; Refresh(); };
        MouseExited += () => { _hover = false; Refresh(); };
        Refresh();
    }

    public void SetCaption(string text)
    {
        Text = text;
        if (_caption != null)
            _caption.Text = text;
    }

    public void SetCaptionColor(Color? color)
    {
        _captionColor = color;
        if (_caption == null)
            return;
        if (color is Color tint)
            _caption.AddThemeColorOverride("font_color", tint);
        else
            _caption.RemoveThemeColorOverride("font_color");
    }

    public void SetPalette(Color fill, Color hover)
    {
        if (_normal != null && _fill == fill && _hoverFill == hover)
            return;
        _fill = fill;
        _hoverFill = hover;
        _normal = CreamTheme.Keycap(fill);
        _over = CreamTheme.Keycap(hover);
        Refresh();
    }

    public void SetPhysicalDown(bool down)
    {
        _physicalDown = down;
        Refresh();
    }

    private void Refresh()
    {
        if (_face == null || _normal == null)
            return;
        int offset = _pointerDown || _physicalDown ? 2 : 0;
        _face.AddThemeStyleboxOverride("panel", _hover || offset != 0 ? _over : _normal);
        CreamTheme.OffsetLayer(_face, offset);
    }
}
