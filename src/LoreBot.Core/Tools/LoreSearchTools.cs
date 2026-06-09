using System.ComponentModel;
using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;

namespace LoreBot.Core.Tools;

public sealed class LoreSearchTools(IEmbeddingService embedder, IVectorSearchService vectorSearch)
{
    [Description("Search for information about characters in a lore universe")]
    public async Task<string> SearchCharactersAsync(
        [Description("Character name or description")] string query,
        [Description("Universe slug: jojo, persona, chainsaw_man")] string universe,
        CancellationToken ct = default)
    {
        var vector = await embedder.EmbedAsync(query, ct);
        var chunks = await vectorSearch.SearchAsync(vector, universe, 5, "characters", ct);
        return FormatChunks(chunks, "No relevant characters found.");
    }

    [Description("Search for information about story events and arcs")]
    public async Task<string> SearchEventsAsync(
        [Description("Event or arc description")] string query,
        [Description("Universe slug: jojo, persona, chainsaw_man")] string universe,
        CancellationToken ct = default)
    {
        var vector = await embedder.EmbedAsync(query, ct);
        var chunks = await vectorSearch.SearchAsync(vector, universe, 5, "events", ct);
        return FormatChunks(chunks, "No relevant events found.");
    }

    [Description("Search for abilities, powers, and mechanics (Stands, Personas, Devils)")]
    public async Task<string> SearchAbilitiesAsync(
        [Description("Ability name or description")] string query,
        [Description("Universe slug: jojo, persona, chainsaw_man")] string universe,
        CancellationToken ct = default)
    {
        var vector = await embedder.EmbedAsync(query, ct);
        var chunks = await vectorSearch.SearchAsync(vector, universe, 5, "ability", ct);
        return FormatChunks(chunks, "No relevant abilities found.");
    }

    [Description("Search for information about locations")]
    public async Task<string> SearchLocationsAsync(
        [Description("Location name or description")] string query,
        [Description("Universe slug: jojo, persona, chainsaw_man")] string universe,
        CancellationToken ct = default)
    {
        var vector = await embedder.EmbedAsync(query, ct);
        var chunks = await vectorSearch.SearchAsync(vector, universe, 5, "locations", ct);
        return FormatChunks(chunks, "No relevant locations found.");
    }

    [Description("Get a full article by its exact title")]
    public async Task<string> GetArticleAsync(
        [Description("Exact article title")] string title,
        [Description("Universe slug: jojo, persona, chainsaw_man")] string universe,
        CancellationToken ct = default)
    {
        var vector = await embedder.EmbedAsync(title, ct);
        var chunks = await vectorSearch.SearchAsync(vector, universe, 1, null, ct);
        return FormatChunks(chunks, "Article not found.");
    }

    private static string FormatChunks(IReadOnlyList<RetrievedChunk> chunks, string emptyMessage)
    {
        if (chunks.Count == 0) return emptyMessage;
        return string.Join("\n\n", chunks.Select(c => $"[{c.Title}]({c.Url})\n{c.ChunkText}"));
    }
}
