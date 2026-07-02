using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoreBot.Core.Configuration;
using LoreBot.Infrastructure.Database;
using LoreBot.Infrastructure.Ingestion;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoreBot.Functions.Functions;

public class AdminFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WikiScraper _scraper;
    private readonly LoreBotOptions _options;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AdminFunction(IServiceScopeFactory scopeFactory, WikiScraper scraper, IOptions<LoreBotOptions> options)
    {
        _scopeFactory = scopeFactory; _scraper = scraper; _options = options.Value;
    }

    public record IndexRequest(string Universe, string WikiApiUrl, int MaxPages = 50, string? StartFrom = null, List<string>? Titles = null);

    [Function("AdminIndex")]
    public async Task<HttpResponseData> IndexAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage/index")] HttpRequestData req,
        FunctionContext ctx)
    {
        var providedKey = req.Headers.TryGetValues("x-admin-key", out var keys)
            ? keys.FirstOrDefault() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrEmpty(_options.AdminApiKey) || !ConstantTimeEquals(providedKey, _options.AdminApiKey))
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        var pipeline = scope.ServiceProvider.GetService<IndexingPipeline>();
        if (db is null || pipeline is null)
        {
            var unavailable = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await unavailable.WriteAsJsonAsync(new
            {
                error = "Indexing is available only when VECTOR_STORE_PROVIDER=postgres."
            });
            return unavailable;
        }

        IndexRequest? dto;
        try
        {
            dto = JsonSerializer.Deserialize<IndexRequest>(await req.ReadAsStringAsync() ?? "", Json);
        }
        catch (JsonException)
        {
            dto = null;
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Universe) || string.IsNullOrWhiteSpace(dto.WikiApiUrl))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("universe and wikiApiUrl are required");
            return bad;
        }

        if (!await IsSafeWikiUrlAsync(dto.WikiApiUrl))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("wikiApiUrl must be a public http(s) address");
            return bad;
        }

        try
        {
            var universe = await db.Universes.FirstOrDefaultAsync(u => u.Slug == dto.Universe, ctx.CancellationToken);
            if (universe is null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync($"Unknown universe '{dto.Universe}'");
                return bad;
            }

            IEnumerable<string> titles = dto.Titles is { Count: > 0 }
                ? dto.Titles
                : (await _scraper.ListAllPagesAsync(dto.WikiApiUrl, dto.StartFrom, ctx.CancellationToken)).Take(dto.MaxPages);

            int indexed = 0;
            var failedTitles = new List<string>();
            foreach (var title in titles)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var (t, text) = await _scraper.GetPlainTextAsync(dto.WikiApiUrl, title, ctx.CancellationToken);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (text.TrimStart().StartsWith("#REDIRECT", StringComparison.OrdinalIgnoreCase)) continue;
                    await pipeline.IndexArticleAsync(universe.Id, t,
                        $"{universe.WikiUrl}/{Uri.EscapeDataString(title)}", "other", text, ctx.CancellationToken);
                    indexed++;
                }
                // A single unreachable/malformed page shouldn't discard progress already made on
                // the rest of the batch; record it and keep going.
                catch (Exception) when (!ctx.CancellationToken.IsCancellationRequested)
                {
                    failedTitles.Add(title);
                }
            }

            var resp = req.CreateResponse(HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(new { indexed, failed = failedTitles });
            return resp;
        }
        catch (Exception) when (!ctx.CancellationToken.IsCancellationRequested)
        {
            var failed = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await failed.WriteAsJsonAsync(new { error = "Indexing failed due to a database or wiki connectivity error." });
            return failed;
        }
    }

    private static bool ConstantTimeEquals(string provided, string expected)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));

    private static async Task<bool> IsSafeWikiUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        if (uri.IsLoopback) return false;

        IPAddress[] addresses;
        try
        {
            addresses = uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
                ? [IPAddress.Parse(uri.Host)]
                : await Dns.GetHostAddressesAsync(uri.Host);
        }
        catch
        {
            return false;
        }

        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            return false;

        if (ip.AddressFamily != AddressFamily.InterNetwork) return true;

        var b = ip.GetAddressBytes();
        if (b[0] == 0) return false;                              // 0.0.0.0/8
        if (b[0] == 10) return false;                              // 10.0.0.0/8
        if (b[0] == 127) return false;                             // 127.0.0.0/8
        if (b[0] == 169 && b[1] == 254) return false;              // 169.254.0.0/16 (incl. cloud IMDS)
        if (b[0] == 172 && b[1] is >= 16 and <= 31) return false;  // 172.16.0.0/12
        if (b[0] == 192 && b[1] == 168) return false;              // 192.168.0.0/16
        return true;
    }
}
