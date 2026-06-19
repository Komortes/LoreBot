using LoreBot.Core.Models;
using LoreBot.Infrastructure.Services;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Services;

public class NoOpCacheServiceTests
{
    [Fact]
    public async Task TryGetAsync_AlwaysMisses()
    {
        var svc = new NoOpCacheService();

        await svc.SetAsync("jojo", "question", [1, 0], new ChatResult { Answer = "cached" });
        var hit = await svc.TryGetAsync("jojo", [1, 0]);

        Assert.Null(hit);
    }
}
