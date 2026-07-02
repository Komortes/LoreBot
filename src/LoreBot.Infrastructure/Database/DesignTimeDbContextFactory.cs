using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoreBot.Infrastructure.Database;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "DATABASE_CONNECTION_STRING must be set to run EF Core design-time commands " +
                "(e.g. dotnet ef migrations add/update). See docker-compose.yml for local dev credentials.");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn, o => o.UseVector())
            .Options;
        return new AppDbContext(options);
    }
}
