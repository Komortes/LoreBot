using System.Security.Cryptography;
using System.Text;
using LoreBot.Core.Abstractions;

namespace LoreBot.Core.Services;

// Hash-projection embedding for local development — no API key required.
// Same text always produces the same vector; overlapping tokens produce similar vectors.
// NOT suitable for production (no semantic understanding).
public class LocalEmbeddingService : IEmbeddingService
{
    private const int Dims = 1536;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Embed(text));

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());

    private static float[] Embed(string text)
    {
        var vector = new float[Dims];
        var tokens = Tokenize(text);

        foreach (var token in tokens)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            for (int i = 0; i < hash.Length - 1; i++)
            {
                int dim = ((hash[i] << 8) | hash[i + 1]) % Dims;
                vector[dim] += (hash[i] & 1) == 0 ? 1f : -1f;
            }
        }

        float mag = MathF.Sqrt(vector.Sum(v => v * v));
        if (mag > 0)
            for (int i = 0; i < Dims; i++)
                vector[i] /= mag;

        return vector;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var word in lower.Split([' ', '\t', '\n', '\r', '.', ',', '!', '?', '-', '_'], StringSplitOptions.RemoveEmptyEntries))
            if (word.Length >= 3) yield return word;
        var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 3).ToArray();
        for (int i = 0; i < words.Length - 1; i++)
            yield return words[i] + "_" + words[i + 1];
        for (int i = 0; i <= lower.Length - 4; i++)
            yield return lower.Substring(i, 4);
    }
}
