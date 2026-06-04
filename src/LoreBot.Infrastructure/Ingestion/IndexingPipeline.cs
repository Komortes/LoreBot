using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;

namespace LoreBot.Infrastructure.Ingestion;

public class IndexingPipeline
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embedder;
    private readonly TextChunker _chunker;

    public IndexingPipeline(AppDbContext db, IEmbeddingService embedder, TextChunker chunker)
    {
        _db = db;
        _embedder = embedder;
        _chunker = chunker;
    }

    public async Task IndexArticleAsync(Guid universeId, string title, string? url,
        string? category, string text, CancellationToken ct = default)
    {
        var chunks = _chunker.Chunk(text);
        if (chunks.Count == 0) return;

        var embeddings = await _embedder.EmbedBatchAsync(chunks.Select(c => c.Text).ToList(), ct);
        for (int i = 0; i < chunks.Count; i++)
        {
            _db.Documents.Add(new Document
            {
                UniverseId = universeId,
                Title = title,
                Url = url,
                Category = category,
                ChunkText = chunks[i].Text,
                ChunkIndex = chunks[i].Index,
                TokenCount = chunks[i].TokenCount,
                Embedding = embeddings[i],
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
