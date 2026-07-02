using System.Net;
using System.Text;
using LoreBot.Infrastructure.Ingestion;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Ingestion;

public class WikiScraperTests
{
    private static readonly TimeSpan[] NoDelay = [TimeSpan.Zero, TimeSpan.Zero];


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

    private sealed class FlakyThenOkHandler : HttpMessageHandler
    {
        private readonly string _response;
        private int _remainingFailures;
        public int Attempts { get; private set; }

        public FlakyThenOkHandler(string response, int failuresBeforeSuccess)
        {
            _response = response;
            _remainingFailures = failuresBeforeSuccess;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Attempts++;
            if (_remainingFailures > 0)
            {
                _remainingFailures--;
                throw new HttpRequestException("simulated transient network failure");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class AlwaysFailsHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Attempts++;
            throw new HttpRequestException("simulated persistent network failure");
        }
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

    [Fact]
    public async Task GetPlainTextAsync_TransientNetworkFailure_RetriesAndSucceeds()
    {
        const string json = """{"query":{"pages":{"1":{"title":"Dio Brando","extract":"Dio Brando is a vampire."}}}}""";
        var handler = new FlakyThenOkHandler(json, failuresBeforeSuccess: 1);
        var scraper = new WikiScraper(new HttpClient(handler), NoDelay);

        var (title, text) = await scraper.GetPlainTextAsync("https://jojo.fandom.com/api.php", "Dio Brando");

        Assert.Equal(2, handler.Attempts);
        Assert.Equal("Dio Brando", title);
        Assert.Contains("vampire", text);
    }

    [Fact]
    public async Task GetPlainTextAsync_PersistentNetworkFailure_GivesUpAfterMaxAttempts()
    {
        var handler = new AlwaysFailsHandler();
        var scraper = new WikiScraper(new HttpClient(handler), NoDelay);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => scraper.GetPlainTextAsync("https://jojo.fandom.com/api.php", "Dio Brando"));

        Assert.Equal(3, handler.Attempts);
    }
}
