using System.Collections.Concurrent;
using LoreBot.Core.Abstractions;

namespace LoreBot.Infrastructure.Services;

public sealed class InMemoryRateLimitService : IRateLimitService
{
    private readonly int _perMinute;
    private readonly int _perHour;
    private readonly int _perDay;
    private readonly ConcurrentDictionary<string, int> _requests = new();

    public InMemoryRateLimitService(int perMinute = 10, int perHour = 50, int perDay = 200)
    {
        _perMinute = perMinute;
        _perHour = perHour;
        _perDay = perDay;
    }

    public Task<RateLimitDecision> CheckAndIncrementAsync(string identifier, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var window in Windows(now))
        {
            var key = $"{identifier}|{window.Type}|{window.Start:O}";
            var count = _requests.AddOrUpdate(key, 1, (_, current) => current + 1);
            if (count > window.Limit)
            {
                return Task.FromResult(new RateLimitDecision(false, $"Rate limit exceeded for window: {window.Type}"));
            }
        }

        RemoveExpired(now);
        return Task.FromResult(new RateLimitDecision(true, null));
    }

    private IEnumerable<(string Type, DateTime Start, int Limit)> Windows(DateTime now)
    {
        yield return ("minute", new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc), _perMinute);
        yield return ("hour", new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc), _perHour);
        yield return ("day", now.Date, _perDay);
    }

    // Each window type expires on its own cadence, not just at day rollover: a minute-window
    // entry is stale as soon as the clock moves past that minute. Without this, minute/hour
    // buckets from earlier today (there's a fresh one per identifier per minute/hour) would
    // never be evicted until the following day, growing the dictionary unbounded.
    private void RemoveExpired(DateTime now)
    {
        var minuteFloor = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        var hourFloor = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var dayFloor = now.Date;

        foreach (var key in _requests.Keys)
        {
            var lastPipe = key.LastIndexOf('|');
            if (lastPipe < 0) continue;
            var typePipe = key.LastIndexOf('|', lastPipe - 1 < 0 ? 0 : lastPipe - 1);
            if (typePipe < 0 || typePipe >= lastPipe) continue;

            var type = key[(typePipe + 1)..lastPipe];
            var startText = key[(lastPipe + 1)..];
            if (!DateTime.TryParse(startText, out var start)) continue;

            var expired = type switch
            {
                "minute" => start < minuteFloor,
                "hour" => start < hourFloor,
                "day" => start < dayFloor,
                _ => start < dayFloor
            };
            if (expired)
            {
                _requests.TryRemove(key, out _);
            }
        }
    }
}
