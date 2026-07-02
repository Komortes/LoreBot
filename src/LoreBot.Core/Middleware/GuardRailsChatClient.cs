using System.Runtime.CompilerServices;
using System.Text;
using LoreBot.Core.GuardRails;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace LoreBot.Core.Middleware;

public class GuardRailsChatClient : DelegatingChatClient
{
    private readonly InputGuardRails _input;
    private readonly OutputGuardRails _output;
    private readonly ILogger<GuardRailsChatClient> _logger;

    private const string BlockedMessage = "Этот запрос нарушает правила использования.";

    public GuardRailsChatClient(IChatClient inner, InputGuardRails input,
        OutputGuardRails output, ILogger<GuardRailsChatClient> logger) : base(inner)
    {
        _input = input;
        _output = output;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        if (userMessage is not null &&
            (_input.IsJailbreakAttempt(userMessage.Text) || _input.IsNsfw(userMessage.Text)))
        {
            _logger.LogWarning("LoreBot.GuardRailBlocked input flagged");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, BlockedMessage));
        }

        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        if (_output.IsModelDisclaimer(response.Text) || !_output.HasCitation(response.Text))
            _logger.LogWarning("LoreBot.OutputFlagged disclaimer or missing citation");

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        if (userMessage is not null &&
            (_input.IsJailbreakAttempt(userMessage.Text) || _input.IsNsfw(userMessage.Text)))
        {
            _logger.LogWarning("LoreBot.GuardRailBlocked input flagged");
            yield return new ChatResponseUpdate(ChatRole.Assistant, BlockedMessage);
            yield break;
        }

        var text = new StringBuilder();
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            text.Append(update.Text);
            yield return update;
        }

        if (_output.IsModelDisclaimer(text.ToString()) || !_output.HasCitation(text.ToString()))
            _logger.LogWarning("LoreBot.OutputFlagged disclaimer or missing citation");
    }
}
