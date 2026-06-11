using System.Text.Json;
using LoreBot.Infrastructure.Database.Repositories;
using LoreBot.Infrastructure.Models;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Database;

public class FileVectorSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsTopMatchesInDescendingSimilarityOrder()
    {
        using var artifact = WriteArtifact(
            Entry("jojo", "Dio", "character", [1, 0]),
            Entry("jojo", "Jonathan", "character", [0, 1]),
            Entry("jojo", "Jotaro", "character", [0.8f, 0.2f]));
        var service = new FileVectorSearchService(artifact.Path, "model-hash", 2);

        var results = await service.SearchAsync([1, 0], "jojo", limit: 2);

        Assert.Collection(
            results,
            result => Assert.Equal("Dio", result.Title),
            result => Assert.Equal("Jotaro", result.Title));
        Assert.True(results[0].Similarity > results[1].Similarity);
    }

    [Fact]
    public async Task SearchAsync_FiltersByUniverseAndCategory()
    {
        using var artifact = WriteArtifact(
            Entry("jojo", "Dio", "character", [1, 0]),
            Entry("jojo", "The World", "ability", [1, 0]),
            Entry("persona-5", "Joker", "character", [1, 0]));
        var service = new FileVectorSearchService(artifact.Path, "model-hash", 2);

        var results = await service.SearchAsync(
            [1, 0],
            "jojo",
            limit: 5,
            category: "ability");

        var result = Assert.Single(results);
        Assert.Equal("The World", result.Title);
    }

    [Fact]
    public void Constructor_DimensionMismatch_Throws()
    {
        using var artifact = WriteArtifact(Entry("jojo", "Dio", "character", [1, 0]));

        var error = Assert.Throws<InvalidDataException>(
            () => new FileVectorSearchService(artifact.Path, "model-hash", 3));

        Assert.Contains("dimension", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ModelHashMismatch_Throws()
    {
        using var artifact = WriteArtifact(Entry("jojo", "Dio", "character", [1, 0]));

        var error = Assert.Throws<InvalidDataException>(
            () => new FileVectorSearchService(artifact.Path, "different-model", 2));

        Assert.Contains("model", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_EntryDimensionMismatch_Throws()
    {
        using var artifact = WriteArtifact(Entry("jojo", "Dio", "character", [1, 0, 0]));

        Assert.Throws<InvalidDataException>(
            () => new FileVectorSearchService(artifact.Path, "model-hash", 2));
    }

    [Fact]
    public async Task SearchAsync_EmptyArtifact_ReturnsEmpty()
    {
        using var artifact = WriteArtifact();
        var service = new FileVectorSearchService(artifact.Path, "model-hash", 2);

        var results = await service.SearchAsync([1, 0], "jojo");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_QueryDimensionMismatch_Throws()
    {
        using var artifact = WriteArtifact(Entry("jojo", "Dio", "character", [1, 0]));
        var service = new FileVectorSearchService(artifact.Path, "model-hash", 2);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchAsync([1, 0, 0], "jojo"));
    }

    [Fact]
    public void Constructor_MissingRequiredMetadata_ThrowsInvalidData()
    {
        using var artifact = WriteRawArtifact(
            """{"schemaVersion":1,"vectorDimension":2,"entries":[]}""");

        Assert.Throws<InvalidDataException>(
            () => new FileVectorSearchService(artifact.Path, "model-hash", 2));
    }

    [Fact]
    public void Constructor_MissingFile_Throws()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"missing-lorebot-rag-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(
            () => new FileVectorSearchService(path, "model-hash", 2));
    }

    private static RagIndexEntry Entry(
        string universe,
        string title,
        string category,
        float[] embedding) =>
        new()
        {
            UniverseSlug = universe,
            Title = title,
            Url = $"https://example.test/{title}",
            Category = category,
            ChunkIndex = 0,
            ChunkText = $"{title} lore",
            Embedding = embedding
        };

    private static TempArtifact WriteArtifact(params RagIndexEntry[] entries)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"lorebot-rag-{Guid.NewGuid():N}.json");
        var artifact = new RagIndexArtifact
        {
            SchemaVersion = RagIndexArtifact.CurrentSchemaVersion,
            EmbeddingModelHash = "model-hash",
            VectorDimension = 2,
            Entries = entries
        };
        File.WriteAllText(path, JsonSerializer.Serialize(artifact));
        return new TempArtifact(path);
    }

    private static TempArtifact WriteRawArtifact(string json)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"lorebot-rag-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return new TempArtifact(path);
    }

    private sealed class TempArtifact(string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose() => File.Delete(Path);
    }
}
