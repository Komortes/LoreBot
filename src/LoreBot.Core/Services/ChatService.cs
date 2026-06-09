using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Core.Tools;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace LoreBot.Core.Services;

public class ChatService : IChatService
{
    private readonly IEmbeddingService _embedder;
    private readonly IVectorSearchService _search;
    private readonly IChatClient _chat;
    private readonly ICacheService? _cache;
    private readonly IReadOnlyList<AITool>? _tools;

    private const double MinSimilarity = 0.75;
    private const int TopK = 8;
    private const int ContextK = 5;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ChatService(IEmbeddingService embedder, IVectorSearchService search,
        IChatClient chat, string universeName = "", ICacheService? cache = null,
        LoreSearchTools? loreTools = null)
    {
        _embedder = embedder;
        _search = search;
        _chat = chat;
        _cache = cache;
        if (loreTools is not null)
        {
            _tools = new List<AITool>
            {
                AIFunctionFactory.Create(loreTools.SearchCharactersAsync),
                AIFunctionFactory.Create(loreTools.SearchEventsAsync),
                AIFunctionFactory.Create(loreTools.SearchAbilitiesAsync),
                AIFunctionFactory.Create(loreTools.SearchLocationsAsync),
                AIFunctionFactory.Create(loreTools.GetArticleAsync),
            };
        }
    }

    public async Task<ChatResult> ChatAsync(string universe, string message, string sessionId, CancellationToken ct = default)
    {
        var queryVector = await _embedder.EmbedAsync(message, ct);

        if (_cache is not null)
        {
            var cached = await _cache.TryGetAsync(universe, queryVector, 0.95, ct);
            if (cached is not null) return cached;
        }

        var retrieved = await _search.SearchAsync(queryVector, universe, TopK, category: null, ct);

        var filtered = retrieved
            .Where(c => c.Similarity >= MinSimilarity)
            .Take(ContextK)
            .ToList();
        if (filtered.Count == 0) filtered = retrieved.Take(ContextK).ToList();

        if (filtered.Count == 0)
        {
            return new ChatResult
            {
                Type = "no_context",
                Answer = "В базе знаний нет информации по этому вопросу.",
                Sources = new(),
                TokensUsed = 0
            };
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, PromptBuilder.BuildSystemPrompt(universe)),
            new(ChatRole.System, PromptBuilder.BuildContextBlock(filtered)),
            new(ChatRole.User, message)
        };

        var chatOptions = new ChatOptions { MaxOutputTokens = 800 };
        if (_tools is { Count: > 0 }) chatOptions.Tools = (IList<AITool>)_tools;
        var response = await _chat.GetResponseAsync(messages, chatOptions, ct);
        var tokens = (int)((response.Usage?.InputTokenCount ?? 0) + (response.Usage?.OutputTokenCount ?? 0));
        var parsed = ParseModelResponse(response.Text);

        var finalResult = new ChatResult
        {
            Type = parsed.Type,
            Answer = parsed.Answer,
            Sources = filtered.Select(c => new Source
            {
                Title = c.Title, Url = c.Url, Category = c.Category, Similarity = c.Similarity
            }).ToList(),
            Confidence = parsed.Confidence,
            Cards = parsed.Cards,
            TokensUsed = tokens
        };

        if (_cache is not null)
            await _cache.SetAsync(universe, message, queryVector, finalResult, ct);

        return finalResult;
    }

    private static ParsedModelResponse ParseModelResponse(string text)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<ParsedModelResponse>(text, Json);
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Answer))
            {
                parsed.Type = NormalizeResponseType(parsed.Type);
                parsed.Cards ??= new();
                return parsed;
            }
        }
        catch (JsonException)
        {
        }

        return new ParsedModelResponse { Type = "answer", Answer = text, Cards = new() };
    }

    private static string NormalizeResponseType(string? type) =>
        type is "answer" or "character" or "timeline" or "comparison" or "no_context" or "guardrail_blocked" or "rate_limited"
            ? type
            : "answer";

    private sealed class ParsedModelResponse
    {
        public string Type { get; set; } = "answer";
        public string Answer { get; set; } = string.Empty;
        public double? Confidence { get; set; }
        public List<ChatCard> Cards { get; set; } = new();
    }
}
