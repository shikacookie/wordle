using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using WordleResearch.Core;

namespace WordleResearch.UI;

public partial class PrototypeGame : Control
{
    private WordRepository _words;
    private BoardLogic[] _boards;
    private BoardLogic Board => _boards[_focus];
    private int _focus;
    private VBoxContainer _content;
    private VBoxContainer _menu;
    private GridContainer _grid;
    private Label _status;
    private LineEdit _draft;
    private int _used;
    private Label _hints;
    private Label _metrics;
    private CancellationTokenSource _analysis;

    public override void _Ready()
    {
        _words = WordRepository.LoadShared();
        var frame = new MarginContainer();
        AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            frame.AddThemeConstantOverride("margin_" + side, 16);
        var stack = new VBoxContainer();
        frame.AddChild(stack);
        _menu = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsVertical = SizeFlags.ExpandFill };
        stack.AddChild(_menu);
        _menu.AddChild(new Label { Text = "Choose a board count", HorizontalAlignment = HorizontalAlignment.Center });
        var modes = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _menu.AddChild(modes);
        foreach (int count in GameConfig.BoardCounts)
            AddButton(modes, count.ToString(), () => Start(count));
        var assist = new CheckButton { Text = "AI assist", ButtonPressed = GameConfig.EnableAiAssist,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        assist.Toggled += value => GameConfig.EnableAiAssist = value;
        _menu.AddChild(assist);
        _content = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false };
        stack.AddChild(_content);
    }

    private void Start(int count)
    {
        GameConfig.BoardCount = count;
        _focus = _used = 0;
        var pool = Enumerable.Range(0, _words.Count).ToArray();
        _boards = new BoardLogic[count];
        for (int i = 0; i < count; i++)
        {
            int j = Random.Shared.Next(i, pool.Length);
            (pool[i], pool[j]) = (pool[j], pool[i]);
            _boards[i] = new BoardLogic(_words, pool[i]);
        }
        _menu.Hide();
        _content.Show();
        var content = _content;
        var top = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddChild(top);
        var boardPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        top.AddChild(boardPanel);
        var board = new VBoxContainer();
        boardPanel.AddChild(board);
        _status = new Label();
        board.AddChild(_status);
        var picker = new HFlowContainer();
        board.AddChild(picker);
        for (int i = 0; i < count; i++)
        {
            int slot = i;
            AddButton(picker, (i + 1).ToString(), () => { _focus = slot; Render(); });
        }
        AddButton(picker, "Menu", () => GetTree().ReloadCurrentScene());
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        board.AddChild(scroll);
        _grid = new GridContainer { Columns = GameConfig.WordLength };
        scroll.AddChild(_grid);
        var side = new PanelContainer { CustomMinimumSize = new Vector2(270, 0) };
        top.AddChild(side);
        var hints = new VBoxContainer();
        side.AddChild(hints);
        _metrics = new Label();
        hints.AddChild(_metrics);
        _hints = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        hints.AddChild(_hints);

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
        RefreshHints();
        _draft.GrabFocus();
    }

    private void Submit()
    {
        if (_boards.All(board => board.IsSolved) || _used >= GameConfig.MaxGuesses)
            return;
        if (!_words.TryGetIndex(_draft.Text, out int index))
        {
            _status.Text = "Not in the word list.";
            return;
        }
        foreach (var board in _boards)
            board.ApplyGuess(index);
        _draft.Clear();
        _used++;
        Render();
        RefreshHints();
    }

    public override void _ExitTree() => _analysis?.Cancel();

    private async void RefreshHints()
    {
        if (!GameConfig.EnableAiAssist)
            return;
        _analysis?.Cancel();
        if (!_draft.Editable)
        {
            _hints.Text = "Game over";
            return;
        }

        using var request = new CancellationTokenSource();
        _analysis = request;
        var token = request.Token;
        var states = _boards.Where(board => !board.IsSolved).Select(board => board.SnapshotCandidates()).ToArray();
        _hints.Text = "Analyzing…";
        try
        {
            var hints = await Task.Run(() => new EntropySolver(_words).Analyze(states, token), token);
            if (!token.IsCancellationRequested && IsInsideTree())
                _hints.Text = string.Join("\n", hints.Select(hint => $"{hint.Word}  {hint.Bits:F2} bits"));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested && IsInsideTree())
                _hints.Text = "Analysis failed";
            GD.PushError(error.ToString());
        }
        finally
        {
            if (_analysis == request)
                _analysis = null;
        }
    }

    private void Render()
    {
        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        _status.Text = $"Board {_focus + 1}/{_boards.Length} · Guess {_used}/{GameConfig.MaxGuesses}"
            + (Board.IsSolved ? " · Solved" : _used >= GameConfig.MaxGuesses ? $" · Answer: {Board.Answer}" : "");
        _draft.Editable = _boards.Any(board => !board.IsSolved) && _used < GameConfig.MaxGuesses;
        _metrics.Text = GameConfig.EnableAiAssist ? $"Ω = {Board.CandidateCount:N0} · S = {Math.Log2(Board.CandidateCount):F2} bits" : "";
        for (int row = 0; row < GameConfig.MaxGuesses; row++)
        for (int col = 0; col < GameConfig.WordLength; col++)
        {
            var tile = new ColorRect { CustomMinimumSize = new Vector2(45, 38), Color = new Color("ededed") };
            var letter = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (row < Board.History.Count)
            {
                var guess = Board.History[row];
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
