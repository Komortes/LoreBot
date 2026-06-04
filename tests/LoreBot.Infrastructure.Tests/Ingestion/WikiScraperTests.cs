using System.Net;
using System.Text;
using LoreBot.Infrastructure.Ingestion;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Ingestion;

public class WikiScraperTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;
        public StubHandler(params string[] responses) => _responses = new Queue<string>(responses);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task ListAllPagesAsync_ParsesTitles()
    {
        const string json = """{"query":{"allpages":[{"title":"Dio Brando"},{"title":"Stand"}]}}""";
        var client = new HttpClient(new StubHandler(json));
        var scraper = new WikiScraper(client);
        var titles = await scraper.ListAllPagesAsync("https://jojo.fandom.com/api.php");
        Assert.Contains("Dio Brando", titles);
        Assert.Contains("Stand", titles);
    }

    [Fact]
    public async Task GetPlainTextAsync_StripsWikiMarkup()
    {
        const string json = """{"query":{"pages":{"1":{"title":"Dio Brando","extract":"Dio Brando is a [[vampire]].\n\n== History ==\nText."}}}}""";
        var client = new HttpClient(new StubHandler(json));
        var scraper = new WikiScraper(client);
        var (title, text) = await scraper.GetPlainTextAsync("https://jojo.fandom.com/api.php", "Dio Brando");
        Assert.Equal("Dio Brando", title);
        Assert.DoesNotContain("[[", text);
        Assert.DoesNotContain("==", text);
        Assert.Contains("vampire", text);
    }
}
