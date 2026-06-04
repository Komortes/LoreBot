using System.Net;
using System.Text.Json;
using LoreBot.Core.Configuration;
using LoreBot.Infrastructure.Database;
using LoreBot.Infrastructure.Ingestion;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoreBot.Functions.Functions;

public class AdminFunction
{
    private readonly AppDbContext _db;
    private readonly WikiScraper _scraper;
    private readonly IndexingPipeline _pipeline;
    private readonly LoreBotOptions _options;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AdminFunction(AppDbContext db, WikiScraper scraper, IndexingPipeline pipeline, IOptions<LoreBotOptions> options)
    {
        _db = db; _scraper = scraper; _pipeline = pipeline; _options = options.Value;
    }

    public record IndexRequest(string Universe, string WikiApiUrl, int MaxPages = 50);

    [Function("AdminIndex")]
    public async Task<HttpResponseData> IndexAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/index")] HttpRequestData req,
        FunctionContext ctx)
    {
        if (!req.Headers.TryGetValues("x-admin-key", out var keys)
            || keys.FirstOrDefault() != _options.AdminApiKey
            || string.IsNullOrEmpty(_options.AdminApiKey))
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        var dto = JsonSerializer.Deserialize<IndexRequest>(await req.ReadAsStringAsync() ?? "", Json)!;
        var universe = await _db.Universes.FirstOrDefaultAsync(u => u.Slug == dto.Universe, ctx.CancellationToken);
        if (universe is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync($"Unknown universe '{dto.Universe}'");
            return bad;
        }

        var titles = (await _scraper.ListAllPagesAsync(dto.WikiApiUrl, ctx.CancellationToken)).Take(dto.MaxPages);
        int indexed = 0;
        foreach (var title in titles)
        {
            var (t, text) = await _scraper.GetPlainTextAsync(dto.WikiApiUrl, title, ctx.CancellationToken);
            if (string.IsNullOrWhiteSpace(text)) continue;
            await _pipeline.IndexArticleAsync(universe.Id, t,
                $"{universe.WikiUrl}/{Uri.EscapeDataString(title)}", "other", text, ctx.CancellationToken);
            indexed++;
        }

        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new { indexed });
        return resp;
    }
}
