using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResponseCacheUniverseIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_response_cache_UniverseId_QuestionHash",
                table: "response_cache",
                columns: new[] { "UniverseId", "QuestionHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_response_cache_UniverseId_QuestionHash",
                table: "response_cache");
        }
    }
}
