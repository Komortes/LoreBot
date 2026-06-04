using LoreBot.Infrastructure.Database;
using LoreBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Services;

public class RateLimitServiceTests
{
    private static AppDbContext NewCtx() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"rl-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task CheckAndIncrementAsync_UnderLimit_Allows()
    {
        using var ctx = NewCtx();
        var svc = new RateLimitService(ctx, perMinute: 10, perHour: 50, perDay: 200);
        var decision = await svc.CheckAndIncrementAsync("1.2.3.4");
        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task CheckAndIncrementAsync_OverMinuteLimit_Blocks()
    {
        using var ctx = NewCtx();
        var svc = new RateLimitService(ctx, perMinute: 3, perHour: 50, perDay: 200);
        for (int i = 0; i < 3; i++) await svc.CheckAndIncrementAsync("5.6.7.8");
        var decision = await svc.CheckAndIncrementAsync("5.6.7.8");
        Assert.False(decision.Allowed);
        Assert.Contains("minute", decision.Reason);
    }
}
