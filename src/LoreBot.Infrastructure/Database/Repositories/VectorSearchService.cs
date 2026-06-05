using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace LoreBot.Infrastructure.Database.Repositories;

public class VectorSearchService : IVectorSearchService
{
    private readonly AppDbContext _db;
    public VectorSearchService(AppDbContext db) => _db = db;

    public async Task<Guid?> ResolveUniverseIdAsync(string slug, CancellationToken ct = default)
    {
        var u = await _db.Universes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, ct);
        return u?.Id;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryVector, string universeSlug, int limit = 8,
        string? category = null, CancellationToken ct = default)
    {
        var universeId = await ResolveUniverseIdAsync(universeSlug, ct);
        if (universeId is null) return Array.Empty<RetrievedChunk>();

        var qv = new Vector(queryVector);
        var query = _db.Documents.AsNoTracking()
            .Where(d => d.UniverseId == universeId && d.Embedding != null);
        if (category is not null)
            query = query.Where(d => d.Category == category);

        // CosineDistance requires Npgsql; for InMemory we just return all matching docs ordered by id
        bool isPostgres = _db.Database.ProviderName?.Contains("Npgsql") == true;
        if (isPostgres)
        {
            var rows = await query
                .OrderBy(d => d.Embedding!.CosineDistance(qv))
                .Take(limit)
                .Select(d => new
                {
                    d.ChunkText,
                    d.Title,
                    d.Url,
                    d.Category,
                    Distance = d.Embedding!.CosineDistance(qv)
                })
                .ToListAsync(ct);
            return rows.Select(r => new RetrievedChunk
            {
                ChunkText = r.ChunkText, Title = r.Title, Url = r.Url,
                Category = r.Category, Similarity = 1.0 - r.Distance
            }).ToList();
        }
        else
        {
            var rows = await query.Take(limit)
                .Select(d => new { d.ChunkText, d.Title, d.Url, d.Category, d.Embedding })
                .ToListAsync(ct);
            return rows
                .Select(r => new RetrievedChunk
            {
                ChunkText = r.ChunkText, Title = r.Title, Url = r.Url,
                Category = r.Category, Similarity = CosineSimilarity(queryVector, r.Embedding!)
            })
                .OrderByDescending(r => r.Similarity)
                .Take(limit)
                .ToList();
        }
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

        return magA == 0 || magB == 0 ? 0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
