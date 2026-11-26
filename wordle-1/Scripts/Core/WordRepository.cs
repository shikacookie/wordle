using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace WordleResearch.Core;

public sealed class WordRepository
{
    public const int PatternCount = 243;
    public const byte SolvedPattern = PatternCount - 1;
    public const string DefaultPath = "res://word_list.json";

    private static WordRepository _shared;
    private readonly Dictionary<string, int> _indices;
    internal Word[] Entries { get; }
    public int Count => Entries.Length;

    private WordRepository(List<Word> words, Dictionary<string, int> indices)
    {
        Entries = words.ToArray();
        _indices = indices;
    }

    // Load on the main thread; the solver only reads the resulting managed data.
    public static WordRepository LoadShared() => _shared ??= Load(DefaultPath);

    public static WordRepository Load(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new InvalidOperationException($"無法開啟 {path}：{FileAccess.GetOpenError()}");
        return FromJson(file.GetAsText());
    }

    public static WordRepository FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("words", out var array) ||
            array.ValueKind != JsonValueKind.Array)
            throw new FormatException("題庫必須包含 words 陣列。");

        var words = new List<Word>(array.GetArrayLength());
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        int position = 0;
        foreach (var element in array.EnumerateArray())
        {
            position++;
            var text = element.ValueKind == JsonValueKind.String
                ? element.GetString()?.Trim().ToUpperInvariant()
                : null;
            if (!IsWord(text))
                throw new FormatException($"題庫第 {position} 筆不是五個英文字母。");
            if (indices.TryAdd(text, words.Count))
                words.Add(new Word(text));
        }

        if (words.Count == 0)
            throw new FormatException("題庫沒有可用單字。");
        return new WordRepository(words, indices);
    }

    public bool TryGetIndex(string text, out int index) =>
        _indices.TryGetValue(text.Trim().ToUpperInvariant(), out index);

    public string GetWord(int index) => Entries[index].Text;

    private static bool IsWord(string text)
    {
        if (text == null || text.Length != GameConfig.WordLength)
            return false;
        foreach (var letter in text)
            if (letter is < 'A' or > 'Z')
                return false;
        return true;
    }

    public static byte EncodePattern(string guess, string answer)
    {
        guess = guess.ToUpperInvariant();
        answer = answer.ToUpperInvariant();
        if (!IsWord(guess) || !IsWord(answer))
            throw new ArgumentException("Pattern encoding requires two five-letter ASCII words.");
        return EncodePattern(new Word(guess), new Word(answer));
    }

    // First letter is the least significant ternary digit: 0 absent, 1 yellow, 2 green.
    public static int GetTile(byte pattern, int position) => (pattern / PowerOfThree[position]) % 3;

    private static readonly int[] PowerOfThree = { 1, 3, 9, 27, 81 };

    internal static byte EncodePattern(in Word guess, in Word answer)
    {
        if (guess.Packed == answer.Packed)
            return SolvedPattern;
        if ((guess.LetterMask & answer.LetterMask) == 0)
            return 0;

        uint remainingAnswer = answer.Packed;
        uint remainingGuess = guess.Packed;
        int pattern = 0;
        // Reserve exact matches before spending any letters on yellow matches.
        for (int i = 0; i < GameConfig.WordLength; i++)
        {
            uint slot = 31u << (i * 5);
            if ((guess.Packed & slot) != (answer.Packed & slot))
                continue;
            remainingAnswer &= ~slot;
            remainingGuess &= ~slot;
            pattern += 2 * PowerOfThree[i];
        }

        for (int i = 0; i < GameConfig.WordLength; i++, remainingGuess >>= 5)
        {
            uint letter = remainingGuess & 31u;
            if (letter == 0 || (answer.LetterMask & (1u << (int)(letter - 1))) == 0)
                continue;
            uint search = remainingAnswer;
            for (int j = 0; j < GameConfig.WordLength; j++, search >>= 5)
            {
                if ((search & 31u) != letter)
                    continue;
                remainingAnswer &= ~(31u << (j * 5));
                pattern += PowerOfThree[i];
                break;
            }
        }
        return (byte)pattern;
    }

    internal readonly struct Word
    {
        public readonly string Text;
        public readonly uint Packed;
        public readonly uint LetterMask;

        public Word(string text)
        {
            Text = text;
            Packed = 0;
            LetterMask = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int letter = text[i] - 'A';
                Packed |= (uint)(letter + 1) << (i * 5);
                LetterMask |= 1u << letter;
            }
        }
    }
}
