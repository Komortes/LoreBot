using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace LoreBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "evaluation_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: true),
                    UniverseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    RetrievedChunksJson = table.Column<string>(type: "text", nullable: true),
                    GroundednessScore = table.Column<double>(type: "double precision", nullable: true),
                    RelevanceScore = table.Column<double>(type: "double precision", nullable: true),
                    GuardrailFlagsJson = table.Column<string>(type: "text", nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rate_limits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<string>(type: "text", nullable: false),
                    WindowType = table.Column<string>(type: "text", nullable: false),
                    RequestCount = table.Column<int>(type: "integer", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_limits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "response_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionHash = table.Column<string>(type: "text", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    QuestionVector = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    AnswerText = table.Column<string>(type: "text", nullable: false),
                    SourcesJson = table.Column<string>(type: "text", nullable: true),
                    HitCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_response_cache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "universes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    WikiUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_universes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_universes_UniverseId",
                        column: x => x.UniverseId,
                        principalTable: "universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_UniverseId",
                table: "documents",
                column: "UniverseId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_UniverseId_Category",
                table: "documents",
                columns: new[] { "UniverseId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_rate_limits_Identifier_WindowType_WindowStart",
                table: "rate_limits",
                columns: new[] { "Identifier", "WindowType", "WindowStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_universes_Slug",
                table: "universes",
                column: "Slug",
                unique: true);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS idx_documents_embedding ON documents " +
                "USING ivfflat (\"Embedding\" vector_cosine_ops) WITH (lists = 100);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS idx_response_cache_vector ON response_cache " +
                "USING ivfflat (\"QuestionVector\" vector_cosine_ops) WITH (lists = 50);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_documents_embedding;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_response_cache_vector;");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "evaluation_logs");

            migrationBuilder.DropTable(
                name: "rate_limits");

            migrationBuilder.DropTable(
                name: "response_cache");

            migrationBuilder.DropTable(
                name: "universes");
        }
    }
}
