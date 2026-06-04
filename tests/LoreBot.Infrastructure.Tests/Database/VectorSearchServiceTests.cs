using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;
using LoreBot.Infrastructure.Database.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Database;

public class VectorSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_UnknownUniverse_ReturnsEmpty()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vs-{Guid.NewGuid()}").Options;
        using var ctx = new AppDbContext(options);
        var svc = new VectorSearchService(ctx);
        var results = await svc.SearchAsync(new float[1536], "nonexistent");
        Assert.Empty(results);
    }

    [Fact]
    public async Task ResolveUniverseIdAsync_ReturnsIdForKnownSlug()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vs2-{Guid.NewGuid()}").Options;
        using var ctx = new AppDbContext(options);
        var u = new Universe { Slug = "jojo", Name = "JoJo" };
        ctx.Universes.Add(u);
        ctx.Documents.Add(new Document
        {
            UniverseId = u.Id, Title = "Dio", ChunkText = "vampire",
            Category = "character", Embedding = new float[1536]
        });
        await ctx.SaveChangesAsync();

        var svc = new VectorSearchService(ctx);
        var id = await svc.ResolveUniverseIdAsync("jojo");
        Assert.Equal(u.Id, id);
    }
}
