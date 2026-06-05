using System.Net;
using LoreBot.Infrastructure.Database;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace LoreBot.Functions.Functions;

public class UniversesFunction
{
    private readonly AppDbContext _db;
    public UniversesFunction(AppDbContext db) => _db = db;

    [Function("Universes")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "universes")] HttpRequestData req,
        FunctionContext ctx)
    {
        var universes = await _db.Universes.AsNoTracking()
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
        var u = await _db.Universes.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, ctx.CancellationToken);
        if (u is null) return req.CreateResponse(HttpStatusCode.NotFound);

        var chunkCount = await _db.Documents.CountAsync(d => d.UniverseId == u.Id, ctx.CancellationToken);
        var articleCount = await _db.Documents.Where(d => d.UniverseId == u.Id)
            .Select(d => d.Title).Distinct().CountAsync(ctx.CancellationToken);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new { slug = u.Slug, articles = articleCount, chunks = chunkCount });
        return resp;
    }
}
