using System.Text.Json;
using LoreBot.Core.Abstractions;
using LoreBot.Infrastructure.Models;

namespace LoreBot.Infrastructure.Ingestion;

public sealed record RagSourceDocument(
    string UniverseSlug,
    string Title,
    string? Url,
    string? Category,
    string Text);

public sealed record RagArtifactWriteResult(
    string ArtifactPath,
    string MetadataPath,
    long ArtifactSizeBytes);

public sealed class RagArtifactBuilder
{
    private const int EmbeddingBatchSize = 64;

    private readonly IEmbeddingService _embedder;
    private readonly TextChunker _chunker;

    public RagArtifactBuilder(IEmbeddingService embedder, TextChunker chunker)
    {
        _embedder = embedder;
        _chunker = chunker;
    }

    public async Task<RagIndexArtifact> BuildAsync(
        IReadOnlyList<RagSourceDocument> documents,
        string embeddingModelHash,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModelHash);

        var pending = documents
            .Where(document => !string.IsNullOrWhiteSpace(document.Text))
            .SelectMany(document => _chunker.Chunk(document.Text)
                .Select(chunk => new PendingEntry(document, chunk)))
            .ToArray();

        if (pending.Length == 0)
        {
            throw new InvalidOperationException("No indexable text chunks were found.");
        }

        var entries = new List<RagIndexEntry>(pending.Length);
        int? vectorDimension = null;

        for (var start = 0; start < pending.Length; start += EmbeddingBatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = pending
                .Skip(start)
                .Take(EmbeddingBatchSize)
                .ToArray();
            var embeddings = await _embedder.EmbedBatchAsync(
                batch.Select(item => item.Chunk.Text).ToArray(),
                ct);

            if (embeddings.Count != batch.Length)
            {
                throw new InvalidOperationException(
                    $"Embedding service returned {embeddings.Count} vectors for {batch.Length} chunks.");
            }

            for (var index = 0; index < batch.Length; index++)
            {
                var embedding = embeddings[index];
                if (embedding.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Embedding service returned an empty vector.");
                }

                vectorDimension ??= embedding.Length;
                if (embedding.Length != vectorDimension)
                {
                    throw new InvalidOperationException(
                        "Embedding service returned vectors with inconsistent dimensions.");
                }

                entries.Add(ToEntry(batch[index], embedding));
            }
        }

        return new RagIndexArtifact
        {
            SchemaVersion = RagIndexArtifact.CurrentSchemaVersion,
            EmbeddingModelHash = embeddingModelHash,
            VectorDimension = vectorDimension ?? 0,
            BuiltAt = DateTimeOffset.UtcNow,
            Entries = entries
        };
    }

    public async Task<RagArtifactWriteResult> WriteAsync(
        RagIndexArtifact artifact,
        string outputDirectory,
        string universeSlug,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(universeSlug);

        Directory.CreateDirectory(outputDirectory);

        var safeSlug = string.Concat(universeSlug.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'
                ? char.ToLowerInvariant(character)
                : '-'));
        var artifactPath = Path.Combine(
            outputDirectory,
            $"lorebot-rag-index.{safeSlug}.json");
        var metadataPath = $"{artifactPath}.metadata.json";

        await using (var stream = File.Create(artifactPath))
        {
            await JsonSerializer.SerializeAsync(stream, artifact, cancellationToken: ct);
        }

        var artifactSize = new FileInfo(artifactPath).Length;
        var metadata = new RagIndexMetadata
        {
            SchemaVersion = artifact.SchemaVersion,
            EmbeddingModelHash = artifact.EmbeddingModelHash,
            VectorDimension = artifact.VectorDimension,
            SourceCount = artifact.Entries
                .Select(entry => (entry.UniverseSlug, entry.Title, entry.Url))
                .Distinct()
                .Count(),
            ChunkCount = artifact.Entries.Count,
            BuiltAt = artifact.BuiltAt,
            ArtifactSizeBytes = artifactSize
        };

        await using (var stream = File.Create(metadataPath))
        {
            await JsonSerializer.SerializeAsync(stream, metadata, cancellationToken: ct);
        }

        return new RagArtifactWriteResult(artifactPath, metadataPath, artifactSize);
    }

    private static RagIndexEntry ToEntry(PendingEntry pending, float[] embedding) =>
        new()
        {
            UniverseSlug = pending.Document.UniverseSlug,
            Title = pending.Document.Title,
            Url = pending.Document.Url,
            Category = pending.Document.Category,
            ChunkIndex = pending.Chunk.Index,
            ChunkText = pending.Chunk.Text,
            Embedding = embedding
        };

    private sealed record PendingEntry(RagSourceDocument Document, TextChunk Chunk);
}
