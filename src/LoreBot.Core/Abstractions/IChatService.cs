using LoreBot.Core.Models;

namespace LoreBot.Core.Abstractions;

public interface IChatService
{
    Task<ChatResult> ChatAsync(string universe, string message, string sessionId, CancellationToken ct = default);
}
