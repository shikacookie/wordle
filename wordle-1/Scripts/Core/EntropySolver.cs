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
            double firstBits = 0, expectedSecondBits = 0;
            foreach (var board in boards)
            {
                var branches = Partition(guess, board, token);
                firstBits += Entropy(branches, board.Length);
                foreach (var branch in branches.Values)
                    if (branch.Count > 1)
                        expectedSecondBits += (double)branch.Count / board.Length * BestFuture(branch, 1, token);
            }
            scores.Add(new GuessRecommendation(_words.GetWord(guess), firstBits, expectedSecondBits));
        }
        return scores.OrderByDescending(score => score.Bits).Take(5).ToArray();
    }

    private double BestFuture(IReadOnlyList<int> states, int depth, CancellationToken token)
    {
        if (depth == 0 || states.Count <= 1)
            return 0;
        double best = 0;
        for (int guess = 0; guess < _words.Count; guess++)
        {
            token.ThrowIfCancellationRequested();
            var branches = Partition(guess, states, token);
            double expected = Entropy(branches, states.Count);
            foreach (var branch in branches.Values)
                expected += (double)branch.Count / states.Count * BestFuture(branch, depth - 1, token);
            best = Math.Max(best, expected);
        }
        return best;
    }

    public double[] GetLetterProbabilities(int[] candidates)
    {
        var probabilities = new double[26];
        foreach (int candidate in candidates)
        {
            uint mask = _words.Entries[candidate].LetterMask;
            for (int letter = 0; letter < probabilities.Length; letter++)
                if ((mask & (1u << letter)) != 0)
                    probabilities[letter] += 1.0 / candidates.Length;
        }
        return probabilities;
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
    public int ForcedBoardCount => 0;
}
