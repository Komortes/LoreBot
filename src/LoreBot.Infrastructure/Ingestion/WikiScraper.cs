using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoreBot.Infrastructure.Ingestion;

public class WikiScraper
{
    private readonly HttpClient _http;
    public WikiScraper(HttpClient http) => _http = http;

    public async Task<List<string>> ListAllPagesAsync(string apiUrl, CancellationToken ct = default)
    {
        var titles = new List<string>();
        string? continueToken = null;
        do
        {
            var url = $"{apiUrl}?action=query&list=allpages&aplimit=500&format=json";
            if (continueToken is not null) url += $"&apcontinue={Uri.EscapeDataString(continueToken)}";
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
            var root = doc.RootElement;
            foreach (var p in root.GetProperty("query").GetProperty("allpages").EnumerateArray())
                titles.Add(p.GetProperty("title").GetString()!);
            continueToken = root.TryGetProperty("continue", out var c)
                && c.TryGetProperty("apcontinue", out var ac) ? ac.GetString() : null;
        } while (continueToken is not null);
        return titles;
    }

    public async Task<(string Title, string Text)> GetPlainTextAsync(string apiUrl, string title, CancellationToken ct = default)
    {
        var url = $"{apiUrl}?action=query&prop=extracts&explaintext=1&format=json&titles={Uri.EscapeDataString(title)}";
        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        var page = doc.RootElement.GetProperty("query").GetProperty("pages").EnumerateObject().First().Value;
        var extract = page.TryGetProperty("extract", out var ex) ? ex.GetString() ?? "" : "";
        return (page.GetProperty("title").GetString()!, CleanWikitext(extract));
    }

    public static string CleanWikitext(string raw)
    {
        var s = Regex.Replace(raw, @"\[\[([^\]|]*\|)?([^\]]*)\]\]", "$2");
        s = Regex.Replace(s, @"\{\{[^}]*\}\}", "");
        s = Regex.Replace(s, @"={2,}\s*([^=]+?)\s*={2,}", "$1");
        s = Regex.Replace(s, @"'''?", "");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }
}
