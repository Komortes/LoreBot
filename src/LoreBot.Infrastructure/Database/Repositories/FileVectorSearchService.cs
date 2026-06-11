using System.Text.Json;
using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Infrastructure.Models;

namespace LoreBot.Infrastructure.Database.Repositories;

public sealed class FileVectorSearchService : IVectorSearchService
{
    private readonly int _vectorDimension;
    private readonly IReadOnlyList<RagIndexEntry> _entries;

    public FileVectorSearchService(
        string artifactPath,
        string expectedEmbeddingModelHash,
        int expectedVectorDimension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEmbeddingModelHash);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedVectorDimension);

        var fullPath = Path.GetFullPath(artifactPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("RAG index artifact was not found.", fullPath);
        }

        RagIndexArtifact artifact;
        try
        {
            using var stream = File.OpenRead(fullPath);
            artifact = JsonSerializer.Deserialize<RagIndexArtifact>(stream)
                ?? throw new InvalidDataException("RAG index artifact is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RAG index artifact contains invalid JSON.", exception);
        }

        ValidateArtifact(artifact, expectedEmbeddingModelHash, expectedVectorDimension);
        _vectorDimension = artifact.VectorDimension;
        _entries = artifact.Entries.ToArray();
    }

    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryVector,
        string universeSlug,
        int limit = 8,
        string? category = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryVector);
        ArgumentException.ThrowIfNullOrWhiteSpace(universeSlug);

        if (queryVector.Length != _vectorDimension)
        {
            throw new ArgumentException(
                $"Query vector dimension {queryVector.Length} does not match artifact dimension {_vectorDimension}.",
                nameof(queryVector));
        }

        if (limit <= 0)
        {
            return Task.FromResult<IReadOnlyList<RetrievedChunk>>([]);
        }

        ct.ThrowIfCancellationRequested();

        var results = _entries
            .Where(entry => entry.UniverseSlug.Equals(
                universeSlug,
                StringComparison.OrdinalIgnoreCase))
            .Where(entry => category is null || string.Equals(
                entry.Category,
                category,
                StringComparison.OrdinalIgnoreCase))
            .Select(entry => new RetrievedChunk
            {
                ChunkText = entry.ChunkText,
                Title = entry.Title,
                Url = entry.Url,
                Category = entry.Category,
                Similarity = CosineSimilarity(queryVector, entry.Embedding)
            })
            .OrderByDescending(entry => entry.Similarity)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<RetrievedChunk>>(results);
    }

    private static void ValidateArtifact(
        RagIndexArtifact artifact,
        string expectedEmbeddingModelHash,
        int expectedVectorDimension)
    {
        if (artifact.SchemaVersion != RagIndexArtifact.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported RAG index schema version {artifact.SchemaVersion}.");
        }

        if (!artifact.EmbeddingModelHash.Equals(
                expectedEmbeddingModelHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "RAG index embedding model hash does not match the configured model.");
        }

        if (artifact.VectorDimension != expectedVectorDimension)
        {
            throw new InvalidDataException(
                $"RAG index vector dimension {artifact.VectorDimension} does not match expected dimension {expectedVectorDimension}.");
        }

        if (artifact.Entries is null)
        {
            throw new InvalidDataException("RAG index entries are missing.");
        }

        var invalidEntry = artifact.Entries.FirstOrDefault(entry =>
            string.IsNullOrWhiteSpace(entry.UniverseSlug)
            || string.IsNullOrWhiteSpace(entry.Title)
            || string.IsNullOrWhiteSpace(entry.ChunkText)
            || entry.Embedding.Length != artifact.VectorDimension);

        if (invalidEntry is not null)
        {
            throw new InvalidDataException(
                "RAG index contains an invalid entry or embedding dimension.");
        }
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        return leftMagnitude == 0 || rightMagnitude == 0
            ? 0
            : dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
