using LoreBot.Core.Models;

namespace LoreBot.Core.Abstractions;

public interface IVectorSearchService
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryVector, string universeSlug, int limit = 8,
        string? category = null, CancellationToken ct = default);
}
