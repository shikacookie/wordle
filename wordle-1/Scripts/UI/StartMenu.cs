using Godot;
using WordleResearch.Core;

namespace WordleResearch.UI;

public partial class StartMenu : Control
{
    public override void _Ready()
    {
        Theme = CreamTheme.Create();
        GetNode<ColorRect>("Background").Color = CreamTheme.Background;
        var modes = GetNode<HBoxContainer>("%BoardCount");
        int selected = GameConfig.BoardCount;
        foreach (int count in GameConfig.BoardCounts)
        {
            var button = new TactileButton
            {
                Text = count.ToString(), CustomMinimumSize = new Vector2(0, 44),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            button.Pressed += () =>
            {
                selected = count;
                foreach (TactileButton mode in modes.GetChildren())
                    SetModePalette(mode, mode == button);
            };
            SetModePalette(button, count == selected);
            modes.AddChild(button);
        }
        var ai = GetNode<CheckButton>("%AiAssist");
        ai.ButtonPressed = GameConfig.EnableAiAssist;
        GetNode<Button>("%Play").Pressed += () =>
        {
            GameConfig.BoardCount = selected;
            GameConfig.EnableAiAssist = ai.ButtonPressed;
            GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
        };
        GetNode<Button>("%Quit").Pressed += () => GetTree().Quit();
    }

    private static void SetModePalette(TactileButton button, bool selected)
    {
        var fill = selected ? CreamTheme.Peach : CreamTheme.Sakura;
        button.SetPalette(fill, fill.Darkened(0.06f));
    }
}
