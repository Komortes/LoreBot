using Pgvector;

namespace LoreBot.Core.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniverseId { get; set; }
    public Universe? Universe { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Category { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TokenCount { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
