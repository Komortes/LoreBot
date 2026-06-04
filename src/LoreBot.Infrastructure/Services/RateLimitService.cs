using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace LoreBot.Infrastructure.Services;

public class RateLimitService : IRateLimitService
{
    private readonly AppDbContext _db;
    private readonly int _perMinute, _perHour, _perDay;

    public RateLimitService(AppDbContext db, int perMinute = 10, int perHour = 50, int perDay = 200)
    {
        _db = db;
        _perMinute = perMinute;
        _perHour = perHour;
        _perDay = perDay;
    }

    public async Task<RateLimitDecision> CheckAndIncrementAsync(string identifier, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var windows = new (string Type, DateTimeOffset Start, int Limit)[]
        {
            ("minute", Floor(now, TimeSpan.FromMinutes(1)), _perMinute),
            ("hour",   Floor(now, TimeSpan.FromHours(1)),   _perHour),
            ("day",    Floor(now, TimeSpan.FromDays(1)),    _perDay),
        };

        var rows = new List<RateLimit>();
        foreach (var w in windows)
        {
            var row = await _db.RateLimits.FirstOrDefaultAsync(
                r => r.Identifier == identifier && r.WindowType == w.Type && r.WindowStart == w.Start, ct);
            if (row is null)
            {
                row = new RateLimit { Identifier = identifier, WindowType = w.Type, WindowStart = w.Start, RequestCount = 0 };
                _db.RateLimits.Add(row);
            }
            if (row.RequestCount >= w.Limit)
                return new RateLimitDecision(false, $"Rate limit exceeded for window: {w.Type}");
            rows.Add(row);
        }

        foreach (var row in rows) row.RequestCount++;
        await _db.SaveChangesAsync(ct);
        return new RateLimitDecision(true, null);
    }

    private static DateTimeOffset Floor(DateTimeOffset t, TimeSpan span)
        => new(t.Ticks - (t.Ticks % span.Ticks), TimeSpan.Zero);
}
