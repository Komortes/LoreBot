using LoreBot.Core.Configuration;
using LoreBot.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Services;

public class LocalModelEmbeddingServiceTests
{
    [Fact]
    public async Task EmbedAsync_NormalizesStableModelVector()
    {
        using var service = BuildService();

        var first = await service.EmbedAsync("Dio Brando");
        var second = await service.EmbedAsync("Dio Brando");

        Assert.Equal(first, second);
        Assert.Equal(1f, Magnitude(first), precision: 5);
    }

    [Fact]
    public async Task EmbedAsync_EmptyText_ThrowsControlledError()
    {
        using var service = BuildService();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.EmbedAsync("   "));

        Assert.Equal("text", error.ParamName);
    }

    [Fact]
    public async Task EmbedBatchAsync_ReturnsOneNormalizedVectorPerInput()
    {
        using var service = BuildService();

        var vectors = await service.EmbedBatchAsync(["Dio", "Jotaro", "Stand"]);

        Assert.Equal(3, vectors.Count);
        Assert.All(vectors, vector => Assert.Equal(1f, Magnitude(vector), precision: 5));
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyCollection_ReturnsEmptyCollection()
    {
        using var service = BuildService();

        var vectors = await service.EmbedBatchAsync([]);

        Assert.Empty(vectors);
    }

    [Fact]
    public async Task EmbedAsync_ModelDimensionMismatchesPostgresColumn_ThrowsControlledError()
    {
        var options = new LoreBotOptions { VectorStoreProvider = "postgres" };
        using var service = new LocalModelEmbeddingService(
            new FakeLocalEmbeddingModel(), NullLogger<LocalModelEmbeddingService>.Instance, options);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EmbedAsync("Dio Brando"));

        Assert.Contains("postgres", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbedAsync_FileVectorStore_SkipsPostgresDimensionCheck()
    {
        var options = new LoreBotOptions { VectorStoreProvider = "file" };
        using var service = new LocalModelEmbeddingService(
            new FakeLocalEmbeddingModel(), NullLogger<LocalModelEmbeddingService>.Instance, options);

        var vector = await service.EmbedAsync("Dio Brando");

        Assert.Equal(3, vector.Length);
    }

    private static LocalModelEmbeddingService BuildService() =>
        new(new FakeLocalEmbeddingModel(), NullLogger<LocalModelEmbeddingService>.Instance);

    private static float Magnitude(float[] vector) =>
        MathF.Sqrt(vector.Sum(value => value * value));

    private sealed class FakeLocalEmbeddingModel : ILocalEmbeddingModel
    {
        public int EmbeddingSize => 3;

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(VectorFor(text));

        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
            IReadOnlyList<string> texts,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(VectorFor).ToArray());

        public void Dispose()
        {
        }

        private static float[] VectorFor(string text) =>
            [text.Length, text[0], 2f];
    }
}
