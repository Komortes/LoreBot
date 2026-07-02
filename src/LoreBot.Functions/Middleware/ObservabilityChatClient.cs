using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace LoreBot.Functions.Middleware;

public sealed class ObservabilityChatClient(IChatClient inner, Func<string> universeProvider) : DelegatingChatClient(inner)
{
    private static readonly ActivitySource Source = new("LoreBot");

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sessionId = options?.AdditionalProperties?.TryGetValue("session_id", out var sid) == true
            ? sid?.ToString() : null;

        using var activity = Source.StartActivity("lorebot.chat.request", ActivityKind.Internal);
        activity?.SetTag("universe.name", universeProvider());
        if (sessionId is not null) activity?.SetTag("user.session_id", sessionId);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await base.GetResponseAsync(messages, options, cancellationToken);
            sw.Stop();
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);
            if (result.Usage is { } usage)
            {
                activity?.SetTag("llm.prompt_tokens", usage.InputTokenCount);
                activity?.SetTag("llm.completion_tokens", usage.OutputTokenCount);
                activity?.SetTag("llm.total_tokens", usage.TotalTokenCount);
            }
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
