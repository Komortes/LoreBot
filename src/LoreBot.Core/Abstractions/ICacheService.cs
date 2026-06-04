using LoreBot.Core.Models;

namespace LoreBot.Core.Abstractions;

public interface ICacheService
{
    Task<ChatResult?> TryGetAsync(string universeSlug, float[] questionVector, double minSimilarity = 0.95, CancellationToken ct = default);
    Task SetAsync(string universeSlug, string question, float[] questionVector, ChatResult result, CancellationToken ct = default);
}
