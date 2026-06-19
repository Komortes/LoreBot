using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;

namespace LoreBot.Infrastructure.Services;

public sealed class NoOpCacheService : ICacheService
{
    public Task<ChatResult?> TryGetAsync(string universeSlug, float[] questionVector, double minSimilarity = 0.95,
        CancellationToken ct = default) =>
        Task.FromResult<ChatResult?>(null);

    public Task SetAsync(string universeSlug, string question, float[] questionVector, ChatResult result,
        CancellationToken ct = default) =>
        Task.CompletedTask;
}
