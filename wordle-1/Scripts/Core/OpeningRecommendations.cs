using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;

namespace WordleResearch.Core;

internal static class OpeningRecommendations
{
    private const string Path = "res://opening_recommendations.json";

    public static GuessRecommendation[] TryLoad(WordRepository repository)
    {
        if (!FileAccess.FileExists(Path))
            return null;

        try
        {
            using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
            if (file == null)
                return null;
            var data = JsonSerializer.Deserialize<OpeningData>(file.GetAsText());
            if (data?.ModelVersion != EntropySolver.OpeningModelVersion ||
                data.Recommendations?.Length != 5)
                return null;

            var words = Enumerable.Range(0, repository.Count).Select(repository.GetWord);
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(string.Join('\n', words))));
            if (fingerprint != data.Fingerprint || data.Recommendations.Any(guess =>
                string.IsNullOrEmpty(guess.Word) || !repository.TryGetIndex(guess.Word, out _) ||
                !double.IsFinite(guess.Bits) || !double.IsFinite(guess.ConvergenceBonus) ||
                !guess.IsCandidate || guess.ForcedBoardCount != 0))
                return null;
            return data.Recommendations;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record OpeningData(int ModelVersion, string Fingerprint, GuessRecommendation[] Recommendations);
}
