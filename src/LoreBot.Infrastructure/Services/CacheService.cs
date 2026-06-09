using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace LoreBot.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly AppDbContext _db;
    public CacheService(AppDbContext db) => _db = db;

    public async Task<ChatResult?> TryGetAsync(string universeSlug, float[] questionVector,
        double minSimilarity = 0.95, CancellationToken ct = default)
    {
        var universe = await _db.Universes.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == universeSlug, ct);
        if (universe is null) return null;

        bool isPostgres = _db.Database.ProviderName?.Contains("Npgsql") == true;

        if (isPostgres)
        {
            var qv = new Vector(questionVector);
            var best = await _db.ResponseCache.AsNoTracking()
                .Where(r => r.UniverseId == universe.Id && r.QuestionVector != null)
                .Select(r => new { r.AnswerText, r.SourcesJson, Distance = r.QuestionVector!.CosineDistance(qv) })
                .OrderBy(x => x.Distance)
                .FirstOrDefaultAsync(ct);

            if (best is null || 1 - best.Distance < minSimilarity) return null;
            return BuildResult(best.AnswerText, best.SourcesJson);
        }
        else
        {
            var rows = await _db.ResponseCache.AsNoTracking()
                .Where(r => r.UniverseId == universe.Id && r.QuestionVector != null)
                .Select(r => new { r.AnswerText, r.SourcesJson, r.QuestionVector })
                .ToListAsync(ct);

            if (rows.Count == 0) return null;

            var best = rows
                .Select(r => new { r.AnswerText, r.SourcesJson, Similarity = CosineSimilarity(questionVector, r.QuestionVector!) })
                .OrderByDescending(x => x.Similarity)
                .First();

            if (best.Similarity < minSimilarity) return null;
            return BuildResult(best.AnswerText, best.SourcesJson);
        }
    }

    public async Task SetAsync(string universeSlug, string question, float[] questionVector,
        ChatResult result, CancellationToken ct = default)
    {
        var universe = await _db.Universes.FirstOrDefaultAsync(u => u.Slug == universeSlug, ct);
        if (universe is null) return;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(question)));

        var exists = await _db.ResponseCache.AnyAsync(
            r => r.UniverseId == universe.Id && r.QuestionHash == hash, ct);
        if (exists) return;

        _db.ResponseCache.Add(new ResponseCache
        {
            UniverseId = universe.Id,
            QuestionHash = hash,
            QuestionText = question,
            QuestionVector = questionVector,
            AnswerText = result.Answer,
            SourcesJson = JsonSerializer.Serialize(result.Sources),
        });
        await _db.SaveChangesAsync(ct);
    }

    private static ChatResult BuildResult(string answer, string? sourcesJson)
    {
        var sources = sourcesJson is null
            ? new List<Source>()
            : JsonSerializer.Deserialize<List<Source>>(sourcesJson) ?? new();
        return new ChatResult { Answer = answer, Sources = sources, FromCache = true };
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return (magA == 0 || magB == 0) ? 0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
