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

    private const double MinSimilarity = 0.30;
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

    public async Task<ChatResult> ChatAsync(string universe, string message, string sessionId,
        IReadOnlyList<(string Role, string Content)>? history = null, CancellationToken ct = default)
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
        };

        if (history is { Count: > 0 })
        {
            foreach (var (role, content) in history.TakeLast(6))
                messages.Add(new ChatMessage(
                    string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User,
                    content));
        }

        messages.Add(new(ChatRole.User, message));

        var chatOptions = new ChatOptions { MaxOutputTokens = 800 };
        if (_tools is { Count: > 0 })
            chatOptions.Tools = _tools.ToList();
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
        var json = StripMarkdownFences(text.Trim());
        try
        {
            var parsed = JsonSerializer.Deserialize<ParsedModelResponse>(json, Json);
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

        return new ParsedModelResponse { Type = "answer", Answer = json, Cards = new() };
    }

    private static string StripMarkdownFences(string text)
    {
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0)
                text = text[(firstNewline + 1)..];
            if (text.EndsWith("```"))
                text = text[..^3].TrimEnd();
        }
        return text.Trim();
    }

    private static string NormalizeResponseType(string? type) =>
        type is "answer" or "character" or "timeline" or "comparison" or "no_context" or "guardrail_blocked" or "rate_limited"
            ? type
            : "answer";

    private sealed class ParsedModelResponse
    {
        public string Type { get; set; } = "answer";
        public string Answer { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDoubleConverter))]
        public double? Confidence { get; set; }
        public List<ChatCard> Cards { get; set; } = new();
    }

    private sealed class FlexibleDoubleConverter : System.Text.Json.Serialization.JsonConverter<double?>
    {
        public override double? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.Number) return reader.GetDouble();
            if (reader.TokenType == System.Text.Json.JsonTokenType.String)
            {
                var s = reader.GetString();
                if (double.TryParse(s, out var v)) return v;
                return s?.ToLower() switch { "high" => 0.9, "medium" => 0.6, "low" => 0.3, _ => null };
            }
            reader.Skip();
            return null;
        }
        public override void Write(System.Text.Json.Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue();
        }
    }
}
