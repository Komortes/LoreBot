using System.Collections.Concurrent;
using System.Reflection;
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

    [Fact]
    public async Task CheckAndIncrementAsync_EvictsStaleMinuteAndHourEntriesFromEarlierToday()
    {
        var svc = new InMemoryRateLimitService(perMinute: 10, perHour: 50, perDay: 200);
        var requests = GetRequestsDictionary(svc);

        // Simulate leftover minute/hour buckets from earlier today (e.g. midnight), which
        // previously only got evicted on the following day.
        var todayMidnight = DateTime.UtcNow.Date;
        requests["1.1.1.1|minute|" + todayMidnight.ToString("O")] = 1;
        requests["1.1.1.1|hour|" + todayMidnight.ToString("O")] = 1;

        // Any request triggers RemoveExpired as a side effect.
        await svc.CheckAndIncrementAsync("2.2.2.2");

        Assert.DoesNotContain(requests.Keys, k => k.StartsWith("1.1.1.1|minute|"));
        Assert.DoesNotContain(requests.Keys, k => k.StartsWith("1.1.1.1|hour|"));
    }

    private static ConcurrentDictionary<string, int> GetRequestsDictionary(InMemoryRateLimitService svc)
    {
        var field = typeof(InMemoryRateLimitService)
            .GetField("_requests", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (ConcurrentDictionary<string, int>)field.GetValue(svc)!;
    }
}
