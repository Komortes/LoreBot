using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using Microsoft.Extensions.AI;

namespace LoreBot.Core.Services;

public class ChatService : IChatService
{
    private readonly IEmbeddingService _embedder;
    private readonly IVectorSearchService _search;
    private readonly IChatClient _chat;
    private readonly string _universeName;

    private const double MinSimilarity = 0.75;
    private const int TopK = 8;
    private const int ContextK = 5;

    public ChatService(IEmbeddingService embedder, IVectorSearchService search,
        IChatClient chat, string universeName)
    {
        _embedder = embedder;
        _search = search;
        _chat = chat;
        _universeName = universeName;
    }

    public async Task<ChatResult> ChatAsync(string universe, string message, string sessionId, CancellationToken ct = default)
    {
        var queryVector = await _embedder.EmbedAsync(message, ct);
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
                Answer = "В базе знаний нет информации по этому вопросу.",
                Sources = new(),
                TokensUsed = 0
            };
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, PromptBuilder.BuildSystemPrompt(_universeName)),
            new(ChatRole.System, PromptBuilder.BuildContextBlock(filtered)),
            new(ChatRole.User, message)
        };

        var response = await _chat.GetResponseAsync(messages, new ChatOptions { MaxOutputTokens = 800 }, ct);
        var tokens = (int)((response.Usage?.InputTokenCount ?? 0) + (response.Usage?.OutputTokenCount ?? 0));

        return new ChatResult
        {
            Answer = response.Text,
            Sources = filtered.Select(c => new Source
            {
                Title = c.Title, Url = c.Url, Category = c.Category, Similarity = c.Similarity
            }).ToList(),
            TokensUsed = tokens
        };
    }
}
