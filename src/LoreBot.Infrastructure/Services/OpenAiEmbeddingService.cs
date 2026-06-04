using LoreBot.Core.Abstractions;
using Microsoft.Extensions.AI;

namespace LoreBot.Infrastructure.Services;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public OpenAiEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator)
        => _generator = generator;

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var result = await _generator.GenerateAsync(new[] { text }, cancellationToken: ct);
        return result[0].Vector.ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var all = new List<float[]>(texts.Count);
        const int batchSize = 100;
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToArray();
            var result = await _generator.GenerateAsync(batch, cancellationToken: ct);
            all.AddRange(result.Select(e => e.Vector.ToArray()));
        }
        return all;
    }
}
