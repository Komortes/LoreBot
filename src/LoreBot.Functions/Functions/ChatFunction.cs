using System.Net;
using System.Text.Json;
using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LoreBot.Functions.Functions;

public class ChatFunction
{
    private readonly IChatService _chatService;
    private readonly IRateLimitService _rateLimit;
    private readonly ILogger<ChatFunction> _logger;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ChatFunction(IChatService chatService, IRateLimitService rateLimit, ILogger<ChatFunction> logger)
    {
        _chatService = chatService;
        _rateLimit = rateLimit;
        _logger = logger;
    }

    [Function("Chat")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var body = await req.ReadAsStringAsync();
        var dto = string.IsNullOrWhiteSpace(body)
            ? null
            : JsonSerializer.Deserialize<ChatRequestDto>(body, Json);

        if (dto is null || string.IsNullOrWhiteSpace(dto.Message) || string.IsNullOrWhiteSpace(dto.Universe))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("universe and message are required");
            return bad;
        }

        var clientIp = req.Headers.TryGetValues("X-Forwarded-For", out var fwd)
            ? fwd.First().Split(',')[0].Trim()
            : "unknown";
        var decision = await _rateLimit.CheckAndIncrementAsync(clientIp, executionContext.CancellationToken);
        if (!decision.Allowed)
        {
            _logger.LogWarning("LoreBot.RateLimitHit ip={Ip} reason={Reason}", clientIp, decision.Reason);
            var limited = req.CreateResponse(HttpStatusCode.OK);
            await limited.WriteAsJsonAsync(new
            {
                type = "rate_limited",
                answer = "Сервис временно перегружен. Попробуй через несколько минут.",
                sources = Array.Empty<object>(),
                confidence = (double?)null,
                cards = Array.Empty<object>(),
                tokensUsed = 0,
                fromCache = false
            });
            return limited;
        }

        var sessionId = string.IsNullOrWhiteSpace(dto.SessionId) ? Guid.NewGuid().ToString() : dto.SessionId;
        _logger.LogInformation("LoreBot.ChatRequest universe={Universe}", dto.Universe);

        ChatResult result;
        try
        {
            result = await _chatService.ChatAsync(dto.Universe, dto.Message, sessionId, executionContext.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoreBot.ChatError universe={Universe}", dto.Universe);
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteAsJsonAsync(new { type = "error", answer = "Произошла ошибка. Попробуй позже.", sources = Array.Empty<object>(), cards = Array.Empty<object>() });
            return err;
        }

        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new
        {
            type = result.Type,
            answer = result.Answer,
            sources = result.Sources,
            confidence = result.Confidence,
            cards = result.Cards,
            tokensUsed = result.TokensUsed,
            fromCache = result.FromCache
        });
        return resp;
    }
}
