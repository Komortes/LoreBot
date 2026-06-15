using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoreBot.Infrastructure.Ingestion;

public class WikiScraper
{
    private readonly HttpClient _http;
    public WikiScraper(HttpClient http) => _http = http;

    public async Task<List<string>> ListAllPagesAsync(string apiUrl, string? startFrom = null, CancellationToken ct = default)
    {
        var titles = new List<string>();
        string? continueToken = startFrom;
        bool firstRequest = true;
        do
        {
            var url = $"{apiUrl}?action=query&list=allpages&aplimit=500&format=json";
            if (continueToken is not null)
                url += firstRequest
                    ? $"&apfrom={Uri.EscapeDataString(continueToken)}"
                    : $"&apcontinue={Uri.EscapeDataString(continueToken)}";
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
            var root = doc.RootElement;
            foreach (var p in root.GetProperty("query").GetProperty("allpages").EnumerateArray())
                titles.Add(p.GetProperty("title").GetString()!);
            firstRequest = false;
            continueToken = root.TryGetProperty("continue", out var c)
                && c.TryGetProperty("apcontinue", out var ac) ? ac.GetString() : null;
        } while (continueToken is not null);
        return titles;
    }

    public async Task<(string Title, string Text)> GetPlainTextAsync(string apiUrl, string title, CancellationToken ct = default)
    {
        var url = $"{apiUrl}?action=parse&page={Uri.EscapeDataString(title)}&prop=wikitext&format=json";
        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out _))
            return (title, "");

        if (TryReadParseResponse(root, title, out var parseTitle, out var wikitext))
            return (parseTitle, CleanWikitext(wikitext));

        if (TryReadQueryExtractResponse(root, title, out var extractTitle, out var extract))
            return (extractTitle, CleanWikitext(extract));

        return (title, "");
    }

    private static bool TryReadParseResponse(JsonElement root, string fallbackTitle, out string title, out string text)
    {
        title = fallbackTitle;
        text = "";

        if (!root.TryGetProperty("parse", out var parse))
            return false;

        if (parse.TryGetProperty("title", out var titleElement))
            title = titleElement.GetString() ?? fallbackTitle;

        if (!parse.TryGetProperty("wikitext", out var wikitext)
            || !wikitext.TryGetProperty("*", out var textElement))
            return false;

        text = textElement.GetString() ?? "";
        return true;
    }

    private static bool TryReadQueryExtractResponse(JsonElement root, string fallbackTitle, out string title, out string text)
    {
        title = fallbackTitle;
        text = "";

        if (!root.TryGetProperty("query", out var query)
            || !query.TryGetProperty("pages", out var pages))
            return false;

        var page = pages.EnumerateObject().FirstOrDefault().Value;
        if (page.ValueKind is JsonValueKind.Undefined)
            return false;

        if (page.TryGetProperty("title", out var titleElement))
            title = titleElement.GetString() ?? fallbackTitle;

        if (!page.TryGetProperty("extract", out var extractElement))
            return false;

        text = extractElement.GetString() ?? "";
        return true;
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
