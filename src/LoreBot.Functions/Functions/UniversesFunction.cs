using System.Net;
using LoreBot.Infrastructure.Database;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBot.Functions.Functions;

public class UniversesFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    public UniversesFunction(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    [Function("Universes")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "universes")] HttpRequestData req,
        FunctionContext ctx)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db is null)
        {
            return await DatabaseProfileRequiredAsync(req);
        }

        var universes = await db.Universes.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { slug = u.Slug, name = u.Name, description = u.Description, wikiUrl = u.WikiUrl })
            .ToListAsync(ctx.CancellationToken);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(universes);
        return resp;
    }

    [Function("UniverseStats")]
    public async Task<HttpResponseData> StatsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "universes/{slug}/stats")] HttpRequestData req,
        string slug, FunctionContext ctx)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db is null)
        {
            return await DatabaseProfileRequiredAsync(req);
        }

        var u = await db.Universes.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, ctx.CancellationToken);
        if (u is null) return req.CreateResponse(HttpStatusCode.NotFound);

        var chunkCount = await db.Documents.CountAsync(d => d.UniverseId == u.Id, ctx.CancellationToken);
        var articleCount = await db.Documents.Where(d => d.UniverseId == u.Id)
            .Select(d => d.Title).Distinct().CountAsync(ctx.CancellationToken);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new { slug = u.Slug, articles = articleCount, chunks = chunkCount });
        return resp;
    }

    private static async Task<HttpResponseData> DatabaseProfileRequiredAsync(HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
        await response.WriteAsJsonAsync(new
        {
            error = "This endpoint requires VECTOR_STORE_PROVIDER=postgres."
        });
        return response;
    }
}
