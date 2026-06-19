using LoreBot.Infrastructure.Services;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Services;

public class InMemoryRateLimitServiceTests
{
    [Fact]
    public async Task CheckAndIncrementAsync_UnderLimit_Allows()
    {
        var svc = new InMemoryRateLimitService(perMinute: 10, perHour: 50, perDay: 200);

        var decision = await svc.CheckAndIncrementAsync("1.2.3.4");

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task CheckAndIncrementAsync_OverMinuteLimit_Blocks()
    {
        var svc = new InMemoryRateLimitService(perMinute: 3, perHour: 50, perDay: 200);

        for (var i = 0; i < 3; i++)
        {
            await svc.CheckAndIncrementAsync("5.6.7.8");
        }

        var decision = await svc.CheckAndIncrementAsync("5.6.7.8");

        Assert.False(decision.Allowed);
        Assert.Contains("minute", decision.Reason);
    }
}
