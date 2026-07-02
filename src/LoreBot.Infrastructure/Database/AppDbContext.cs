using LoreBot.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace LoreBot.Infrastructure.Database;

public class AppDbContext : DbContext
{
    /// <summary>Fixed dimension of the Postgres <c>vector</c> columns; any embedding provider paired
    /// with VECTOR_STORE_PROVIDER=postgres must produce vectors of exactly this size.</summary>
    public const int EmbeddingDimension = 1536;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Universe> Universes => Set<Universe>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<RateLimit> RateLimits => Set<RateLimit>();
    public DbSet<ResponseCache> ResponseCache => Set<ResponseCache>();
    public DbSet<EvaluationLog> EvaluationLogs => Set<EvaluationLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        bool isPostgres = Database.ProviderName?.Contains("Npgsql") == true;

        if (isPostgres) b.HasPostgresExtension("vector");

        var toVector = new ValueConverter<float[]?, Vector?>(
            arr => arr == null ? null : new Vector(arr),
            vec => vec == null ? null : vec.Memory.ToArray());

        b.Entity<Universe>(e =>
        {
            e.ToTable("universes");
            e.HasIndex(u => u.Slug).IsUnique();
        });

        b.Entity<Document>(e =>
        {
            e.ToTable("documents");
            var p = e.Property(d => d.Embedding);
            if (isPostgres) p.HasConversion(toVector).HasColumnType($"vector({EmbeddingDimension})");
            e.HasIndex(d => d.UniverseId);
            e.HasIndex(d => new { d.UniverseId, d.Category });
            e.HasOne(d => d.Universe).WithMany(u => u.Documents)
                .HasForeignKey(d => d.UniverseId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RateLimit>(e =>
        {
            e.ToTable("rate_limits");
            e.HasIndex(r => new { r.Identifier, r.WindowType, r.WindowStart }).IsUnique();
        });

        b.Entity<ResponseCache>(e =>
        {
            e.ToTable("response_cache");
            var p = e.Property(r => r.QuestionVector);
            if (isPostgres) p.HasConversion(toVector).HasColumnType($"vector({EmbeddingDimension})");
            // Covers both CacheService.TryGetAsync (filters by UniverseId) and
            // SetAsync's exact-duplicate lookup (UniverseId, QuestionHash).
            e.HasIndex(r => new { r.UniverseId, r.QuestionHash });
        });

        b.Entity<EvaluationLog>(e => e.ToTable("evaluation_logs"));
    }
}
