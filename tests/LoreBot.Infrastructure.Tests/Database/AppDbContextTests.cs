using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Database;

public class AppDbContextTests
{
    [Fact]
    public void Model_HasAllExpectedEntitySets()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("model-test").Options;
        using var ctx = new AppDbContext(options);

        Assert.NotNull(ctx.Model.FindEntityType(typeof(Universe)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Document)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(RateLimit)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(ResponseCache)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(EvaluationLog)));
    }

    [Fact]
    public void Document_EmbeddingColumn_IsVector1536_OnPostgres()
    {
        // Use Npgsql context to verify vector column type metadata (no real connection needed)
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=test", o => o.UseVector())
            .Options;
        using var ctx = new AppDbContext(options);
        var prop = ctx.Model.FindEntityType(typeof(Document))!.FindProperty(nameof(Document.Embedding))!;
        Assert.Equal("vector(1536)", prop.GetColumnType());
    }
}
