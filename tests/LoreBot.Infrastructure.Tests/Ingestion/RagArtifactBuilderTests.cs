using System.Text.Json;
using LoreBot.Core.Abstractions;
using LoreBot.Infrastructure.Ingestion;
using LoreBot.Infrastructure.Models;
using NSubstitute;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Ingestion;

public class RagArtifactBuilderTests
{
    [Fact]
    public async Task BuildAsync_ChunksDocumentsAndWritesCompatibleArtifact()
    {
        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var texts = (IReadOnlyList<string>)call[0];
                return Task.FromResult<IReadOnlyList<float[]>>(
                    texts.Select((_, index) => new[] { 1f, index + 1f }).ToArray());
            });
        var builder = new RagArtifactBuilder(embedder, new TextChunker(20, 0));
        var documents = new[]
        {
            new RagSourceDocument(
                "jojo",
                "Dio Brando",
                "https://example.test/dio",
                "character",
                string.Join(' ', Enumerable.Range(0, 40).Select(index => $"word{index}"))),
            new RagSourceDocument(
                "jojo",
                "The World",
                null,
                "ability",
                "The World stops time.")
        };

        var artifact = await builder.BuildAsync(documents, "model-hash");

        Assert.Equal("model-hash", artifact.EmbeddingModelHash);
        Assert.Equal(2, artifact.VectorDimension);
        Assert.True(artifact.Entries.Count > documents.Length);
        Assert.All(artifact.Entries, entry => Assert.Equal(2, entry.Embedding.Length));
        Assert.Contains(artifact.Entries, entry =>
            entry.Title == "The World" && entry.Category == "ability");
    }

    [Fact]
    public async Task BuildAsync_EmbeddingCountMismatch_Throws()
    {
        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>([]));
        var builder = new RagArtifactBuilder(embedder, new TextChunker());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildAsync(
                [new RagSourceDocument("jojo", "Dio", null, null, "Dio lore")],
                "model-hash"));
    }

    [Fact]
    public async Task BuildAsync_EmptyEmbeddingVector_Throws()
    {
        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>([[]]));
        var builder = new RagArtifactBuilder(embedder, new TextChunker());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildAsync(
                [new RagSourceDocument("jojo", "Dio", null, null, "Dio lore")],
                "model-hash"));
    }

    [Fact]
    public async Task WriteAsync_WritesArtifactAndMetadataSidecar()
    {
        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>([new[] { 1f, 0f }]));
        var builder = new RagArtifactBuilder(embedder, new TextChunker());
        var artifact = await builder.BuildAsync(
            [new RagSourceDocument("jojo", "Dio", null, "character", "Dio lore")],
            "model-hash");
        var directory = Path.Combine(Path.GetTempPath(), $"lorebot-index-{Guid.NewGuid():N}");

        try
        {
            var result = await builder.WriteAsync(artifact, directory, "jojo");

            Assert.True(File.Exists(result.ArtifactPath));
            Assert.True(File.Exists(result.MetadataPath));
            Assert.True(result.ArtifactSizeBytes > 0);

            var metadata = JsonSerializer.Deserialize<RagIndexMetadata>(
                await File.ReadAllTextAsync(result.MetadataPath));
            Assert.NotNull(metadata);
            Assert.Equal(1, metadata.SourceCount);
            Assert.Equal(1, metadata.ChunkCount);
            Assert.Equal(result.ArtifactSizeBytes, metadata.ArtifactSizeBytes);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
