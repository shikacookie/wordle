using System;
using Godot;
using WordleResearch.Core;

namespace WordleResearch.UI;

public partial class PrototypeGame : Control
{
    private WordRepository _words;
    private BoardLogic _board;
    private GridContainer _grid;
    private Label _status;
    private LineEdit _draft;
    private int _used;

    public override void _Ready()
    {
        _words = WordRepository.LoadShared();
        _board = new BoardLogic(_words, Random.Shared.Next(_words.Count));

        var frame = new MarginContainer();
        AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            frame.AddThemeConstantOverride("margin_" + side, 16);
        var content = new VBoxContainer();
        frame.AddChild(content);
        var top = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddChild(top);
        var boardPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        top.AddChild(boardPanel);
        var board = new VBoxContainer();
        boardPanel.AddChild(board);
        _status = new Label();
        board.AddChild(_status);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        board.AddChild(scroll);
        _grid = new GridContainer { Columns = GameConfig.WordLength };
        scroll.AddChild(_grid);
        top.AddChild(new PanelContainer { CustomMinimumSize = new Vector2(270, 0) });

        var keyboard = new VBoxContainer();
        content.AddChild(keyboard);
        _draft = new LineEdit { MaxLength = GameConfig.WordLength, PlaceholderText = "Type a guess" };
        _draft.TextSubmitted += _ => Submit();
        keyboard.AddChild(_draft);
        foreach (string keys in new[] { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" })
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            keyboard.AddChild(row);
            foreach (char key in keys)
                AddButton(row, key.ToString(), () =>
                {
                    if (_draft.Editable && _draft.Text.Length < GameConfig.WordLength)
                        _draft.Text += key;
                });
            if (keys == "ZXCVBNM")
            {
                AddButton(row, "DEL", () =>
                {
                    if (_draft.Editable && _draft.Text.Length > 0)
                        _draft.Text = _draft.Text[..^1];
                });
                AddButton(row, "ENTER", Submit);
            }
        }
        Render();
        _draft.GrabFocus();
    }

    private void Submit()
    {
        if (_board.IsSolved || _used >= GameConfig.MaxGuesses)
            return;
        if (!_words.TryGetIndex(_draft.Text, out int index))
        {
            _status.Text = "Not in the word list.";
            return;
        }
        _board.ApplyGuess(index);
        _draft.Clear();
        _used++;
        Render();
    }

    private void Render()
    {
        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        _status.Text = $"Guess {_used}/{GameConfig.MaxGuesses}"
            + (_board.IsSolved ? " · Solved" : _used >= GameConfig.MaxGuesses ? $" · Answer: {_board.Answer}" : "");
        _draft.Editable = !_board.IsSolved && _used < GameConfig.MaxGuesses;
        for (int row = 0; row < GameConfig.MaxGuesses; row++)
        for (int col = 0; col < GameConfig.WordLength; col++)
        {
            var tile = new ColorRect { CustomMinimumSize = new Vector2(45, 38), Color = new Color("ededed") };
            var letter = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (row < _board.History.Count)
            {
                var guess = _board.History[row];
                letter.Text = guess.Word[col].ToString();
                tile.Color = WordRepository.GetTile(guess.Pattern, col) switch
                {
                    2 => new Color("538d4e"),
                    1 => new Color("b59f3b"),
                    _ => new Color("787c7e")
                };
            }
            _grid.AddChild(tile);
            tile.AddChild(letter);
            letter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }

    private static void AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(36, 30), FocusMode = FocusModeEnum.None };
        button.Pressed += action;
        parent.AddChild(button);
    }
}
