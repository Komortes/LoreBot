using System.Runtime.CompilerServices;
using LoreBot.Core.Abstractions;
using Microsoft.Extensions.AI;

namespace LoreBot.Core.Middleware;

public class RateLimitingChatClient : DelegatingChatClient
{
    private readonly IRateLimitService _limiter;
    private readonly Func<string> _identifierProvider;

    private const string DegradedMessage =
        "Сервис временно перегружен. Пожалуйста, попробуй ещё раз через несколько минут.";

    public RateLimitingChatClient(IChatClient inner, IRateLimitService limiter, Func<string> identifierProvider)
        : base(inner)
    {
        _limiter = limiter;
        _identifierProvider = identifierProvider;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var decision = await _limiter.CheckAndIncrementAsync(_identifierProvider(), cancellationToken);
        if (!decision.Allowed)
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, DegradedMessage));
        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var decision = await _limiter.CheckAndIncrementAsync(_identifierProvider(), cancellationToken);
        if (!decision.Allowed)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, DegradedMessage);
            yield break;
        }

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            yield return update;
        }
    }
}
