using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Core.Tools;
using NSubstitute;
using Xunit;

public class LoreSearchToolsTests
{
    private readonly IVectorSearchService _search = Substitute.For<IVectorSearchService>();
    private readonly IEmbeddingService _embedder = Substitute.For<IEmbeddingService>();
    private readonly LoreSearchTools _tools;
    private readonly float[] _fakeVector = new float[1536];

    public LoreSearchToolsTests()
    {
        _embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_fakeVector);
        _tools = new LoreSearchTools(_embedder, _search);
    }

    [Fact]
    public async Task SearchCharacters_ReturnsFormattedChunks()
    {
        _search.SearchAsync(_fakeVector, "jojo", 5, "characters", Arg.Any<CancellationToken>())
               .Returns(new List<RetrievedChunk>
               {
                   new() { ChunkText = "Dio Brando is a vampire", Title = "Dio Brando", Url = "https://jojowiki.com/Dio", Similarity = 0.9f }
               });

        var result = await _tools.SearchCharactersAsync("Дио", "jojo");

        Assert.Contains("Dio Brando", result);
        await _search.Received(1).SearchAsync(_fakeVector, "jojo", 5, "characters", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchEvents_ReturnsEmptyMessage_WhenNoResults()
    {
        _search.SearchAsync(_fakeVector, "jojo", 5, "events", Arg.Any<CancellationToken>())
               .Returns(new List<RetrievedChunk>());

        var result = await _tools.SearchEventsAsync("battle", "jojo");

        Assert.Equal("No relevant events found.", result);
    }

    [Fact]
    public async Task SearchAbilities_ReturnsFormattedChunks()
    {
        _search.SearchAsync(_fakeVector, "jojo", 5, "ability", Arg.Any<CancellationToken>())
               .Returns(new List<RetrievedChunk>
               {
                   new() { ChunkText = "The World stops time", Title = "The World", Url = "https://jojowiki.com/The_World", Similarity = 0.95f }
               });

        var result = await _tools.SearchAbilitiesAsync("стенд Дио", "jojo");

        Assert.Contains("The World", result);
        Assert.Contains("stops time", result);
    }

    [Fact]
    public async Task SearchLocations_ReturnsEmptyMessage_WhenNoResults()
    {
        _search.SearchAsync(_fakeVector, "persona", 5, "locations", Arg.Any<CancellationToken>())
               .Returns(new List<RetrievedChunk>());

        var result = await _tools.SearchLocationsAsync("Метавселенная", "persona");

        Assert.Equal("No relevant locations found.", result);
    }

    [Fact]
    public async Task GetArticle_ReturnsFirstChunkText()
    {
        _search.SearchAsync(_fakeVector, "jojo", 1, null, Arg.Any<CancellationToken>())
               .Returns(new List<RetrievedChunk>
               {
                   new() { ChunkText = "Full article text", Title = "Dio Brando", Url = "https://jojowiki.com/Dio", Similarity = 1.0f }
               });

        var result = await _tools.GetArticleAsync("Dio Brando", "jojo");

        Assert.Contains("Full article text", result);
    }

    [Fact]
    public async Task SearchCharacters_FormatsMultipleChunks()
    {
        _search.SearchAsync(_fakeVector, "jojo", 5, "characters", Arg.Any<CancellationToken>())
               .Returns(new List<RetrievedChunk>
               {
                   new() { ChunkText = "Dio is a vampire", Title = "Dio Brando", Url = "https://jojowiki.com/Dio", Similarity = 0.9f },
                   new() { ChunkText = "Jotaro is a student", Title = "Jotaro Kujo", Url = "https://jojowiki.com/Jotaro", Similarity = 0.85f }
               });

        var result = await _tools.SearchCharactersAsync("главный герой", "jojo");

        Assert.Contains("Dio Brando", result);
        Assert.Contains("Jotaro Kujo", result);
    }
}
