using System.Data;
using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LoreBot.Infrastructure.Services;

public class RateLimitService : IRateLimitService
{
    private const int MaxAttempts = 5;
    private const string UniqueViolation = "23505";
    private const string SerializationFailure = "40001";

    private readonly AppDbContext _db;
    private readonly int _perMinute, _perHour, _perDay;

    public RateLimitService(AppDbContext db, int perMinute = 10, int perHour = 50, int perDay = 200)
    {
        _db = db;
        _perMinute = perMinute;
        _perHour = perHour;
        _perDay = perDay;
    }

    // Two concurrent requests for the same identifier can otherwise both read
    // "under limit" before either commits its increment, or both try to insert
    // the same brand-new window row. On a relational provider we run the
    // check-then-increment inside a SERIALIZABLE transaction and retry on the
    // conflict Postgres reports, which closes both races. The EF Core InMemory
    // provider used by unit tests doesn't support transactions, so it falls
    // back to the original single-attempt behavior.
    public async Task<RateLimitDecision> CheckAndIncrementAsync(string identifier, CancellationToken ct = default)
    {
        var isRelational = _db.Database.IsRelational();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            IDbContextTransaction? tx = null;
            if (isRelational)
                tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                var decision = await CheckAndIncrementCoreAsync(identifier, ct);
                if (tx is not null) await tx.CommitAsync(ct);
                return decision;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsRetryableConflict(ex))
            {
                if (tx is not null) await tx.RollbackAsync(ct);
                foreach (var entry in _db.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;
            }
            finally
            {
                if (tx is not null) await tx.DisposeAsync();
            }
        }

        throw new InvalidOperationException(
            $"Rate limit check for '{identifier}' failed after {MaxAttempts} attempts due to repeated write conflicts.");
    }

    private async Task<RateLimitDecision> CheckAndIncrementCoreAsync(string identifier, CancellationToken ct)
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

    private static bool IsRetryableConflict(Exception ex)
    {
        var pg = ex switch
        {
            PostgresException direct => direct,
            DbUpdateException { InnerException: PostgresException inner } => inner,
            _ => null
        };
        return pg is not null && pg.SqlState is UniqueViolation or SerializationFailure;
    }

    private static DateTimeOffset Floor(DateTimeOffset t, TimeSpan span)
        => new(t.Ticks - (t.Ticks % span.Ticks), TimeSpan.Zero);
}
