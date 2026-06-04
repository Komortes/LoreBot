using System.Net;
using System.Text.Json;
using LoreBot.Core.Abstractions;
using LoreBot.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LoreBot.Functions.Functions;

public class ChatFunction
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatFunction> _logger;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ChatFunction(IChatService chatService, ILogger<ChatFunction> logger)
    {
        _chatService = chatService;
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

        var sessionId = string.IsNullOrWhiteSpace(dto.SessionId) ? Guid.NewGuid().ToString() : dto.SessionId;
        _logger.LogInformation("LoreBot.ChatRequest universe={Universe}", dto.Universe);

        var result = await _chatService.ChatAsync(dto.Universe, dto.Message, sessionId, executionContext.CancellationToken);

        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new
        {
            answer = result.Answer,
            sources = result.Sources,
            tokensUsed = result.TokensUsed,
            fromCache = result.FromCache
        });
        return resp;
    }
}
