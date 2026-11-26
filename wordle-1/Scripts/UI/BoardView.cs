using System;
using Godot;
using WordleResearch.Core;

namespace WordleResearch.UI;

internal sealed class BoardView
{
    private const int CellSize = 56;
    private const int Gap = 6;
    private const int VisibleRows = 6;
    private const int ViewportPadding = 4;
    private enum Tile { Empty, Draft, Absent, Present, Exact }

    private readonly Panel[,] _cells;
    private readonly Label[,] _letters;
    private readonly StyleBoxFlat[] _styles =
    {
        CreamTheme.Keycap(new Color("FFFAF6")),
        CreamTheme.Keycap(new Color("FCE0E8")),
        CreamTheme.Keycap(CreamTheme.Gray),
        CreamTheme.Keycap(CreamTheme.Gold),
        CreamTheme.Keycap(CreamTheme.Mint)
    };
    private readonly ScrollContainer _scroll;
    private readonly Label _state;

    public BoardView(ScrollContainer scroll, GridContainer grid, Label state, int maxGuesses)
    {
        _scroll = scroll;
        _state = state;
        bool canScroll = maxGuesses > VisibleRows;
        _scroll.VerticalScrollMode = canScroll ? ScrollContainer.ScrollMode.Reserve : ScrollContainer.ScrollMode.Disabled;
        _scroll.CustomMinimumSize = new Vector2(
            GameConfig.WordLength * (CellSize + Gap) - Gap + 3 + (canScroll ? 16 : 0),
            VisibleRows * CellSize + (VisibleRows - 1) * Gap + ViewportPadding);
        grid.Columns = GameConfig.WordLength;
        grid.AddThemeConstantOverride("h_separation", Gap);
        grid.AddThemeConstantOverride("v_separation", Gap);
        _cells = new Panel[maxGuesses, GameConfig.WordLength];
        _letters = new Label[maxGuesses, GameConfig.WordLength];
        var shadow = CreamTheme.Keycap(CreamTheme.Cocoa);
        for (int row = 0; row < maxGuesses; row++)
        for (int col = 0; col < GameConfig.WordLength; col++)
        {
            var cell = new Control
            {
                CustomMinimumSize = new Vector2(CellSize, CellSize),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            CreamTheme.AddKeycapLayer(cell, shadow, 3);
            var face = CreamTheme.AddKeycapLayer(cell, _styles[0], 0);
            var letter = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            letter.AddThemeFontSizeOverride("font_size", 25);
            face.AddChild(letter);
            letter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            grid.AddChild(cell);
            _cells[row, col] = face;
            _letters[row, col] = letter;
        }
    }

    public void Render(BoardLogic board, string draft, bool ended, bool showCandidates)
    {
        _state.Text = board.IsSolved ? "SOLVED" : ended ? $"ANSWER: {board.Answer}"
            : showCandidates ? FormattableString.Invariant($"{board.CandidateCount:N0} LEFT") : "UNSOLVED";

        for (int row = 0; row < _letters.GetLength(0); row++)
        for (int col = 0; col < GameConfig.WordLength; col++)
        {
            var tile = Tile.Empty;
            string text = "";
            if (row < board.History.Count)
            {
                var guess = board.History[row];
                text = guess.Word[col].ToString();
                tile = WordRepository.GetTile(guess.Pattern, col) switch
                {
                    2 => Tile.Exact,
                    1 => Tile.Present,
                    _ => Tile.Absent
                };
            }
            else if (!board.IsSolved && !ended && row == board.History.Count && col < draft.Length)
            {
                text = draft[col].ToString();
                tile = Tile.Draft;
            }

            var label = _letters[row, col];
            label.Text = text;
            label.AddThemeColorOverride("font_color", tile >= Tile.Absent ? Colors.White : CreamTheme.Ink);
            // A soft outline keeps white feedback letters readable on yellow and mint.
            label.AddThemeColorOverride("font_outline_color", CreamTheme.Ink);
            label.AddThemeConstantOverride("outline_size", tile >= Tile.Absent ? 2 : 0);
            _cells[row, col].AddThemeStyleboxOverride("panel", _styles[(int)tile]);
        }
    }

    public void ScrollToLatest(BoardLogic board, bool ended)
    {
        int row = Math.Clamp(board.History.Count - (board.IsSolved || ended ? 1 : 0), 0, _cells.GetLength(0) - 1);
        _scroll.ScrollVertical = Math.Max(0, row - VisibleRows + 1) * (CellSize + Gap);
    }
}
