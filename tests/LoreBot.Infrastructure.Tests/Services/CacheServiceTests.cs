using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;
using LoreBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Services;

public class CacheServiceTests
{
    [Fact]
    public async Task SetThenTryGet_ExactVector_ReturnsCachedAnswer()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cache-{Guid.NewGuid()}").Options;
        using var ctx = new AppDbContext(options);
        var u = new Universe { Slug = "jojo", Name = "JoJo" };
        ctx.Universes.Add(u);
        await ctx.SaveChangesAsync();

        var svc = new CacheService(ctx);
        var vec = new float[1536];
        vec[0] = 1f;
        await svc.SetAsync("jojo", "Кто такой Дио?", vec,
            new ChatResult { Answer = "Дио — вампир [1].", TokensUsed = 10 });

        var hit = await svc.TryGetAsync("jojo", vec, minSimilarity: 0.95);
        Assert.NotNull(hit);
        Assert.Equal("Дио — вампир [1].", hit!.Answer);
        Assert.True(hit.FromCache);
    }

    [Fact]
    public async Task TryGet_DissimilarVector_ReturnsNull()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cache2-{Guid.NewGuid()}").Options;
        using var ctx = new AppDbContext(options);
        var u = new Universe { Slug = "jojo", Name = "JoJo" };
        ctx.Universes.Add(u);
        await ctx.SaveChangesAsync();

        var svc = new CacheService(ctx);
        var stored = new float[1536]; stored[0] = 1f;
        await svc.SetAsync("jojo", "q", stored, new ChatResult { Answer = "a" });

        var query = new float[1536]; query[1] = 1f; // orthogonal
        var hit = await svc.TryGetAsync("jojo", query, minSimilarity: 0.95);
        Assert.Null(hit);
    }
}
