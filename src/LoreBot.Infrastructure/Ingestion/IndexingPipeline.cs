using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;

namespace LoreBot.Infrastructure.Ingestion;

public class IndexingPipeline
{
    // Matches RagArtifactBuilder's batch size: keeps each embedding call within
    // provider batch/token limits instead of sending an entire article's chunks at once.
    private const int EmbeddingBatchSize = 64;

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

        for (var start = 0; start < chunks.Count; start += EmbeddingBatchSize)
        {
            var batch = chunks.Skip(start).Take(EmbeddingBatchSize).ToList();
            var embeddings = await _embedder.EmbedBatchAsync(batch.Select(c => c.Text).ToList(), ct);
            for (int i = 0; i < batch.Count; i++)
            {
                _db.Documents.Add(new Document
                {
                    UniverseId = universeId,
                    Title = title,
                    Url = url,
                    Category = category,
                    ChunkText = batch[i].Text,
                    ChunkIndex = batch[i].Index,
                    TokenCount = batch[i].TokenCount,
                    Embedding = embeddings[i],
                });
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
