using System;
using System.Collections.Generic;
using Godot;
using WordleResearch.Core;

namespace WordleResearch.UI;

public partial class VirtualKeyboard : VBoxContainer
{
    private readonly Dictionary<string, TactileButton> _keys = new(StringComparer.Ordinal);
    public event Action<char> LetterPressed;
    public event Action DeletePressed;
    public event Action EnterPressed;

    public override void _Ready()
    {
        foreach (string letters in new[] { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" })
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            AddChild(row);
            bool bottom = letters == "ZXCVBNM";
            if (bottom)
                AddKey(row, "ENTER", () => EnterPressed?.Invoke(), 76, primary: true);
            foreach (char letter in letters)
                AddKey(row, letter.ToString(), () => LetterPressed?.Invoke(letter), 44);
            if (bottom)
                AddKey(row, "DEL", () => DeletePressed?.Invoke(), 76);
        }
    }

    internal void SetPhysicalKey(InputEventKey key)
    {
        long letter = key.Unicode != 0 ? key.Unicode : (long)key.Keycode;
        string caption = key.Keycode switch
        {
            Key.Enter or Key.KpEnter => "ENTER",
            Key.Backspace => "DEL",
            _ when letter is >= 'a' and <= 'z' or >= 'A' and <= 'Z' => char.ToUpperInvariant((char)letter).ToString(),
            _ => ""
        };
        if (_keys.TryGetValue(caption, out var cap))
            cap.SetPhysicalDown(key.Pressed);
    }

    internal void ReleasePhysicalKeys()
    {
        foreach (var key in _keys.Values)
            key.SetPhysicalDown(false);
    }

    internal void ShowFeedback(IReadOnlyList<GuessFeedback> history)
    {
        Span<int> best = stackalloc int[26];
        best.Clear(); // Zero means unplayed; actual tile feedback starts at one here.
        foreach (var guess in history)
            for (int position = 0; position < guess.Word.Length; position++)
            {
                int index = guess.Word[position] - 'A';
                int feedback = WordRepository.GetTile(guess.Pattern, position) + 1;
                best[index] = Math.Max(best[index], feedback);
            }

        foreach (var (caption, key) in _keys)
        {
            if (caption.Length != 1)
                continue;
            int feedback = best[caption[0] - 'A'];
            var fill = feedback switch
            {
                3 => CreamTheme.Mint,
                2 => CreamTheme.Gold,
                1 => CreamTheme.Gray,
                _ => CreamTheme.Sakura
            };
            key.SetPalette(fill, feedback == 0 ? new Color("F9E2E9") : fill.Darkened(0.06f));
            key.SetCaptionColor(feedback == 0 ? null : Colors.White);
        }
    }

    private void AddKey(HBoxContainer row, string text, Action pressed, int width, bool primary = false)
    {
        var key = new TactileButton
        {
            Text = text,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(width, 36)
        };
        key.SetPalette(primary ? CreamTheme.Peach : CreamTheme.Sakura,
            primary ? new Color("EE98A9") : new Color("F9E2E9"));
        key.Pressed += pressed;
        _keys.Add(text, key);
        row.AddChild(key);
    }
}
