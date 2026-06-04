namespace LoreBot.Core.Abstractions;

public record RateLimitDecision(bool Allowed, string? Reason);

public interface IRateLimitService
{
    Task<RateLimitDecision> CheckAndIncrementAsync(string identifier, CancellationToken ct = default);
}
