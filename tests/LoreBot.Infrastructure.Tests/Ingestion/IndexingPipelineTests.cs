using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;
using LoreBot.Infrastructure.Ingestion;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Ingestion;

public class IndexingPipelineTests
{
    [Fact]
    public async Task IndexArticleAsync_PersistsChunksWithEmbeddings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"idx-{Guid.NewGuid()}").Options;
        using var ctx = new AppDbContext(options);
        var universe = new Universe { Slug = "jojo", Name = "JoJo" };
        ctx.Universes.Add(universe);
        await ctx.SaveChangesAsync();

        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<float[]>>(
                ((IReadOnlyList<string>)ci[0]).Select(_ => new float[1536]).ToList()));

        var pipeline = new IndexingPipeline(ctx, embedder, new TextChunker(50, 10));
        await pipeline.IndexArticleAsync(universe.Id, "Dio Brando",
            "https://jojo/Dio", "character",
            string.Join(" ", Enumerable.Range(0, 200).Select(i => $"word{i}")));

        Assert.True(await ctx.Documents.CountAsync() > 1);
        Assert.All(await ctx.Documents.ToListAsync(), d =>
        {
            Assert.Equal("Dio Brando", d.Title);
            Assert.NotNull(d.Embedding);
        });
    }

    [Fact]
    public async Task IndexArticleAsync_LargeArticle_EmbedsInBatchesOfAtMost64()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"idx-{Guid.NewGuid()}").Options;
        using var ctx = new AppDbContext(options);
        var universe = new Universe { Slug = "jojo", Name = "JoJo" };
        ctx.Universes.Add(universe);
        await ctx.SaveChangesAsync();

        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<float[]>>(
                ((IReadOnlyList<string>)ci[0]).Select(_ => new float[1536]).ToList()));

        // Tiny chunks push a 400-word article well past 64 chunks, forcing multiple batches.
        var pipeline = new IndexingPipeline(ctx, embedder, new TextChunker(maxTokens: 4, overlapTokens: 0));
        var text = string.Join(" ", Enumerable.Range(0, 400).Select(i => $"word{i}"));
        await pipeline.IndexArticleAsync(universe.Id, "Dio Brando", null, "character", text);

        var batchSizes = embedder.ReceivedCalls()
            .Select(call => ((IReadOnlyList<string>)call.GetArguments()[0]!).Count)
            .ToList();

        Assert.True(batchSizes.Count > 1, "expected multiple embedding batches for a large article");
        Assert.All(batchSizes, size => Assert.True(size <= 64));
    }
}
