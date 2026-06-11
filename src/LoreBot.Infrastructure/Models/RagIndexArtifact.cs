namespace LoreBot.Infrastructure.Models;

public sealed class RagIndexArtifact
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string EmbeddingModelHash { get; set; } = string.Empty;
    public int VectorDimension { get; set; }
    public DateTimeOffset BuiltAt { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<RagIndexEntry> Entries { get; set; } = [];
}

public sealed class RagIndexEntry
{
    public string UniverseSlug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Category { get; set; }
    public int ChunkIndex { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
}

public sealed class RagIndexMetadata
{
    public int SchemaVersion { get; set; }
    public string EmbeddingModelHash { get; set; } = string.Empty;
    public int VectorDimension { get; set; }
    public int SourceCount { get; set; }
    public int ChunkCount { get; set; }
    public DateTimeOffset BuiltAt { get; set; }
    public long ArtifactSizeBytes { get; set; }
}
