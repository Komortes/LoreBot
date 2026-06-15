using LoreBot.Core.Models;

namespace LoreBot.Core.Abstractions;

public interface IChatService
{
    Task<ChatResult> ChatAsync(string universe, string message, string sessionId,
        IReadOnlyList<(string Role, string Content)>? history = null, CancellationToken ct = default);
}
