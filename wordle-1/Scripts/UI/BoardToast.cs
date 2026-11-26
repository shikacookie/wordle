using Godot;

namespace WordleResearch.UI;

internal sealed class BoardToast
{
    private readonly Control _sticker;
    private readonly Label _label;
    private readonly ScrollContainer _board;
    private readonly Control _grid;
    private Tween _fade;
    private Tween _shake;

    public BoardToast(Control overlay, ScrollContainer board, Control grid)
    {
        _board = board;
        _grid = grid;
        _sticker = new Control { Name = "BoardToast", ZIndex = 10,
            MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        overlay.AddChild(_sticker);
        CreamTheme.AddKeycapLayer(_sticker, CreamTheme.Keycap(CreamTheme.Cocoa), 3);
        var face = CreamTheme.AddKeycapLayer(_sticker, CreamTheme.Keycap(CreamTheme.Cocoa), 0);
        _label = new Label { HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeFontSizeOverride("font_size", 15);
        face.AddChild(_label);
        _label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.Resized += Reposition;
        _board.GetParent<Control>().ItemRectChanged += Reposition;
    }

    public void Show(string message, bool shake)
    {
        _fade?.Kill();
        _label.Text = message;
        _sticker.Size = new Vector2(_label.GetCombinedMinimumSize().X + 24, 32);
        _sticker.Modulate = Colors.White;
        _sticker.Show();
        Reposition();
        _fade = _sticker.CreateTween();
        _fade.TweenProperty(_sticker, "modulate:a", 0d, 0.22).SetDelay(1.5)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _fade.TweenCallback(Callable.From(_sticker.Hide));
        if (!shake)
            return;

        _shake?.Kill();
        _board.Position = Vector2.Zero;
        _shake = _board.CreateTween();
        foreach (int offset in new[] { -7, 7, -5, 4, -2, 0 })
            _shake.TweenProperty(_board, "position:x", offset, 0.045);
    }

    private void Reposition()
    {
        var stage = _board.GetParent<Control>();
        _sticker.GlobalPosition = new Vector2(
            stage.GlobalPosition.X + (_grid.Size.X - _sticker.Size.X) / 2,
            stage.GlobalPosition.Y - _sticker.Size.Y - 6);
    }
}
