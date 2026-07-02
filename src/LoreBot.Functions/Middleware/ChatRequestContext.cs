namespace LoreBot.Functions.Middleware;

// Scoped per HTTP request. The chat-client pipeline (built once per request, before the
// request body is parsed) reads Universe lazily through this holder so telemetry can tag
// spans with the real per-request universe instead of a value fixed at DI-registration time.
public sealed class ChatRequestContext
{
    public string? Universe { get; set; }
}
