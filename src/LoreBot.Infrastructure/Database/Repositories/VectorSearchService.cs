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
                .Select(d => new { d.ChunkText, d.Title, d.Url, d.Category })
                .ToListAsync(ct);
            return rows.Select(r => new RetrievedChunk
            {
                ChunkText = r.ChunkText, Title = r.Title, Url = r.Url,
                Category = r.Category, Similarity = 1.0
            }).ToList();
        }
        else
        {
            var rows = await query.Take(limit)
                .Select(d => new { d.ChunkText, d.Title, d.Url, d.Category })
                .ToListAsync(ct);
            return rows.Select(r => new RetrievedChunk
            {
                ChunkText = r.ChunkText, Title = r.Title, Url = r.Url,
                Category = r.Category, Similarity = 1.0
            }).ToList();
        }
    }
}
