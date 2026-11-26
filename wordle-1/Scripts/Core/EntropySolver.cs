using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WordleResearch.Core;

public sealed class EntropySolver
{
    private readonly WordRepository _words;

    public EntropySolver(WordRepository words) => _words = words;

    public GuessRecommendation[] Analyze(IReadOnlyList<int[]> boards, CancellationToken token = default)
    {
        if (boards.Count == 0)
            return Array.Empty<GuessRecommendation>();
        var scores = new List<GuessRecommendation>(_words.Count);
        for (int guess = 0; guess < _words.Count; guess++)
        {
            token.ThrowIfCancellationRequested();
            double bits = 0;
            foreach (var board in boards)
                bits += Entropy(Partition(guess, board, token), board.Length);
            scores.Add(new GuessRecommendation(_words.GetWord(guess), bits, 0));
        }
        return scores.OrderByDescending(score => score.Bits).Take(5).ToArray();
    }

    private Dictionary<byte, List<int>> Partition(int guess, IReadOnlyList<int> candidates, CancellationToken token)
    {
        var branches = new Dictionary<byte, List<int>>();
        foreach (int candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            byte pattern = WordRepository.EncodePattern(_words.Entries[guess], _words.Entries[candidate]);
            if (!branches.TryGetValue(pattern, out var branch))
                branches[pattern] = branch = new List<int>();
            branch.Add(candidate);
        }
        return branches;
    }

    private static double Entropy(Dictionary<byte, List<int>> branches, int states)
    {
        double sum = 0;
        foreach (var branch in branches.Values)
        {
            double p = (double)branch.Count / states;
            sum -= p * Math.Log2(p);
        }
        return sum;
    }
}

public readonly record struct GuessRecommendation(string Word, double FirstBits, double ExpectedSecondBits)
{
    public double Bits => FirstBits + ExpectedSecondBits;
}
