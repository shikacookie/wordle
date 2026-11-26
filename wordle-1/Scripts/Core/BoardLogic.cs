using System;
using System.Collections.Generic;
using System.Linq;

namespace WordleResearch.Core;

public sealed class BoardLogic
{
    private readonly WordRepository _repository;
    private readonly int _answerIndex;
    private readonly List<int> _candidates;
    private readonly List<GuessFeedback> _history = new();

    public IReadOnlyList<GuessFeedback> History { get; }
    public int CandidateCount => _candidates.Count;
    public bool IsSolved { get; private set; }
    public string Answer => _repository.GetWord(_answerIndex);

    public BoardLogic(WordRepository repository, int answerIndex)
    {
        if ((uint)answerIndex >= (uint)repository.Count)
            throw new ArgumentOutOfRangeException(nameof(answerIndex));
        _repository = repository;
        _answerIndex = answerIndex;
        _candidates = new List<int>(Enumerable.Range(0, repository.Count));
        History = _history.AsReadOnly();
    }

    public void ApplyGuess(int guessIndex)
    {
        if (IsSolved)
            return;
        var entries = _repository.Entries;
        var guess = entries[guessIndex];
        byte pattern = WordRepository.EncodePattern(guess, entries[_answerIndex]);
        _history.Add(new GuessFeedback(guess.Text, pattern));
        _candidates.RemoveAll(index => WordRepository.EncodePattern(guess, entries[index]) != pattern);
        IsSolved = pattern == WordRepository.SolvedPattern;
    }

    public int[] SnapshotCandidates() => _candidates.ToArray();
}

public readonly record struct GuessFeedback(string Word, byte Pattern);
