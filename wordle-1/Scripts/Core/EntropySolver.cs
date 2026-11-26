using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace WordleResearch.Core;

public sealed class EntropySolver
{
    internal const int OpeningModelVersion = 2;
    private const int BeamWidth = 15;
    private const int AnswersOnlyLimit = 3;
    private const double ConvergenceWeight = 0.15;
    private const double DirectSolveWeight = 0.15;
    private readonly WordRepository _repository;

    public EntropySolver(WordRepository repository) => _repository = repository;

    public double[] GetLetterProbabilities(int[] candidates)
    {
        var probabilities = new double[26];
        if (candidates.Length == 0)
            return probabilities;

        var words = _repository.Entries;
        foreach (int candidate in candidates)
        {
            // Count the presence of a letter once per word, regardless of duplicates.
            for (uint mask = words[candidate].LetterMask; mask != 0; mask &= mask - 1)
                probabilities[BitOperations.TrailingZeroCount(mask)]++;
        }
        for (int i = 0; i < probabilities.Length; i++)
            probabilities[i] /= candidates.Length;
        return probabilities;
    }

    // Callers supply snapshots of unsolved boards only.
    public GuessRecommendation[] Analyze(IReadOnlyList<int[]> unsolvedBoards,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var words = _repository.Entries;
        var solveChance = new double[words.Length];
        int answerCount = 0;
        var forcedBoards = new int[words.Length];
        var groups = new List<CandidateGroup>();
        foreach (var candidates in unsolvedBoards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (int candidate in candidates)
            {
                if (solveChance[candidate] == 0)
                    answerCount++;
                solveChance[candidate] += 1.0 / candidates.Length;
            }

            if (candidates.Length == 1)
                forcedBoards[candidates[0]]++;
            if (candidates.Length <= 1)
                continue; // A known answer contributes zero entropy for every guess.

            // Identical snapshots (especially at the opening) need only one histogram.
            var group = groups.Find(group => candidates.AsSpan().SequenceEqual(group.Candidates));
            if (group == null)
                groups.Add(new CandidateGroup(candidates));
            else
                group.BoardCount++;
        }

        var forcedGuesses = Enumerable.Range(0, words.Length).Where(index => forcedBoards[index] > 0).ToArray();
        bool answersOnly = answerCount <= AnswersOnlyLimit;
        if (forcedGuesses.Length > 0 && (!answersOnly || groups.Count == 0))
            return forcedGuesses.OrderByDescending(index => forcedBoards[index])
                .ThenBy(index => words[index].Text, StringComparer.Ordinal)
                .Select(index => new GuessRecommendation(words[index].Text, 0, 0, 0, true, forcedBoards[index]))
                .ToArray();
        if (groups.Count == 0)
            return Array.Empty<GuessRecommendation>();

        var allGuesses = Enumerable.Range(0, words.Length);
        var firstGuesses = answersOnly ? allGuesses.Where(index => solveChance[index] > 0).ToArray() : null;

        int maxWorkers = Math.Clamp(Environment.ProcessorCount, 1, 4);
        var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = maxWorkers };
        int largestBoard = groups.Max(group => group.Candidates.Length);
        var countLogs = new double[largestBoard + 1];
        for (int i = 2; i < countLogs.Length; i++)
            countLogs[i] = i * Math.Log2(i);
        var scores = new double[words.Length];
        Parallel.For(0, firstGuesses?.Length ?? words.Length, options, position =>
        {
            Span<int> counts = stackalloc int[WordRepository.PatternCount];
            int index = firstGuesses?[position] ?? position;
            var guess = words[index];
            double totalEntropy = 0;
            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                counts.Clear();
                foreach (int candidate in group.Candidates)
                    counts[WordRepository.EncodePattern(guess, words[candidate])]++;

                double loss = 0;
                foreach (int count in counts)
                    loss += countLogs[count];
                totalEntropy += (group.StateEntropy - loss / group.Candidates.Length) * group.BoardCount;
            }
            scores[index] = totalEntropy;
        });

        // Reward the expected number of directly solved boards, not merely informative feedback.
        // At the opening every word has the same bonus, so the precomputed opening order is unchanged.
        var beam = (firstGuesses ?? allGuesses)
            .OrderByDescending(index => Math.Round(scores[index] + DirectSolveWeight * solveChance[index], 12))
            .ThenByDescending(index => solveChance[index] > 0)
            .ThenBy(index => words[index].Text, StringComparer.Ordinal)
            .Take(BeamWidth).ToArray();
        var results = new GuessRecommendation[beam.Length];
        int workerCount = Math.Min(maxWorkers, beam.Length);
        var bucketStorage = new int[workerCount * largestBoard];
        var matrix = largestBoard > 2 ? _repository.PreparePatternMatrix() : null;

        Parallel.For(0, workerCount, options, worker =>
        {
            Span<int> counts = stackalloc int[WordRepository.PatternCount];
            Span<int> offsets = stackalloc int[WordRepository.PatternCount];
            Span<int> histogram = stackalloc int[WordRepository.PatternCount];
            var buckets = bucketStorage.AsSpan(worker * largestBoard, largestBoard);
            for (int position = worker; position < beam.Length; position += workerCount)
            {
                int index = beam[position];
                double expectedSecond = 0;
                double convergence = 0;
                foreach (var group in groups)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    counts.Clear();
                    foreach (int candidate in group.Candidates)
                        counts[WordRepository.EncodePattern(words[index], words[candidate])]++;

                    int start = 0;
                    for (int pattern = 0; pattern < counts.Length; pattern++)
                    {
                        offsets[pattern] = start;
                        start += counts[pattern];
                    }
                    foreach (int candidate in group.Candidates)
                        buckets[offsets[WordRepository.EncodePattern(words[index], words[candidate])]++] = candidate;

                    for (int pattern = 0; pattern < counts.Length; pattern++)
                    {
                        int length = counts[pattern];
                        if (length == 0)
                            continue;
                        double weight = (double)length / group.Candidates.Length * group.BoardCount;
                        if (length == 1)
                            convergence += weight;
                        else
                            expectedSecond += weight * BestSecondEntropy(
                                buckets.Slice(offsets[pattern] - length, length), beam,
                                matrix, countLogs, histogram, cancellationToken);
                    }
                }
                results[position] = new GuessRecommendation(words[index].Text, scores[index], expectedSecond,
                    convergence * ConvergenceWeight, solveChance[index] > 0, forcedBoards[index]);
            }
        });

        // Bonuses affect ranking, not the reported information in bits.
        return results.Select((result, position) => (Result: result,
                Score: result.Bits + result.ConvergenceBonus + DirectSolveWeight * solveChance[beam[position]]))
            .OrderByDescending(entry => entry.Result.ForcedBoardCount > 0)
            .ThenByDescending(entry => Math.Round(entry.Score, 12))
            .ThenByDescending(entry => entry.Result.IsCandidate)
            .ThenBy(entry => entry.Result.Word, StringComparer.Ordinal).Take(5)
            .Select(entry => entry.Result)
            .ToArray();
    }

    private double BestSecondEntropy(ReadOnlySpan<int> branch, int[] beam, int[] matrix,
        double[] countLogs, Span<int> counts, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (branch.Length == 2)
            return 1; // Guessing either candidate separates the two states.

        double bestLoss = countLogs[branch.Length];
        // Only try the beam and the answers still possible for this board after the feedback.
        foreach (int index in beam)
        {
            bestLoss = Math.Min(bestLoss, SecondGuessLoss(index, branch, matrix, countLogs, counts, token));
            if (bestLoss == 0)
                return Math.Log2(branch.Length);
        }
        foreach (int index in branch)
        {
            if (Array.IndexOf(beam, index) < 0)
            {
                bestLoss = Math.Min(bestLoss, SecondGuessLoss(index, branch, matrix, countLogs, counts, token));
                if (bestLoss == 0)
                    break;
            }
        }
        return Math.Log2(branch.Length) - bestLoss / branch.Length;
    }

    private double SecondGuessLoss(int guess, ReadOnlySpan<int> branch, int[] matrix, double[] countLogs,
        Span<int> counts, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        counts.Clear();
        foreach (int answer in branch)
            counts[_repository.GetCachedPattern(matrix, guess, answer)]++;
        double loss = 0;
        foreach (int count in counts)
            loss += countLogs[count];
        return loss;
    }

    private sealed class CandidateGroup
    {
        public int[] Candidates { get; }
        public double StateEntropy { get; }
        public int BoardCount { get; set; } = 1;

        public CandidateGroup(int[] candidates)
        {
            Candidates = candidates;
            StateEntropy = Math.Log2(candidates.Length);
        }
    }
}

public readonly record struct GuessRecommendation(string Word, double FirstBits, double ExpectedSecondBits,
    double ConvergenceBonus, bool IsCandidate, int ForcedBoardCount)
{
    public double Bits => FirstBits + ExpectedSecondBits;
}
