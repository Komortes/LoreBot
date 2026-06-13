using LoreBot.Core.Services;
using LoreBot.Infrastructure.Database.Repositories;
using LoreBot.Infrastructure.Ingestion;
using Xunit;

namespace LoreBot.Infrastructure.Tests.RagQuality;

/// <summary>
/// Deterministic retrieval-quality gate for the file-backed vector store. Uses the hash-projection
/// embedder (no gguf model required) so it runs in CI, while still asserting that the file store
/// ranks the most relevant chunk first for fixed queries and returns results in similarity order.
/// </summary>
public sealed class LocalRagRetrievalTests : IAsyncLifetime
{
    private const string ModelHash = "test-model-hash";

    private static readonly (string Title, string Category, string Text)[] Corpus =
    [
        ("Dio Brando", "character",
            "Dio Brando is a vampire and the main antagonist of the first arc. "
            + "He wields the powerful Stand called The World, which can stop time."),
        ("Jotaro Kujo", "character",
            "Jotaro Kujo is a stoic high school delinquent and the protagonist of Stardust Crusaders. "
            + "His Stand is the precise and immensely strong Star Platinum."),
        ("Hamon", "ability",
            "Hamon, also known as Ripple, is a martial-arts breathing technique "
            + "that channels sunlight energy to fight vampires."),
        ("Morioh", "location",
            "Morioh is a quiet Japanese town and the primary setting of Diamond is Unbreakable."),
    ];

    private readonly LocalEmbeddingService _embedder = new();
    private string _tempDir = string.Empty;
    private FileVectorSearchService _search = null!;
    private int _dimension;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "lorebot-rag-" + Guid.NewGuid().ToString("N"));
        var builder = new RagArtifactBuilder(_embedder, new TextChunker(maxTokens: 400, overlapTokens: 50));
        var documents = Corpus
            .Select(item => new RagSourceDocument("jojo", item.Title, $"https://jojo/{item.Title}", item.Category, item.Text))
            .ToArray();

        var artifact = await builder.BuildAsync(documents, ModelHash);
        var written = await builder.WriteAsync(artifact, _tempDir, "jojo");

        _dimension = artifact.VectorDimension;
        _search = new FileVectorSearchService(written.ArtifactPath, ModelHash, _dimension);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("vampire antagonist wields the Stand The World stop time", "Dio Brando")]
    [InlineData("protagonist Stardust Crusaders Stand Star Platinum", "Jotaro Kujo")]
    [InlineData("breathing technique sunlight energy ripple vampires", "Hamon")]
    public async Task TopResult_MatchesExpectedDocument(string query, string expectedTitle)
    {
        var queryVector = await _embedder.EmbedAsync(query);

        var results = await _search.SearchAsync(queryVector, "jojo", limit: 3);

        Assert.NotEmpty(results);
        Assert.Equal(expectedTitle, results[0].Title);
    }

    [Fact]
    public async Task Results_AreOrderedByDescendingSimilarity()
    {
        var queryVector = await _embedder.EmbedAsync("Dio Brando vampire Stand The World");

        var results = await _search.SearchAsync(queryVector, "jojo", limit: Corpus.Length);

        var similarities = results.Select(result => result.Similarity).ToArray();
        Assert.Equal(similarities.OrderByDescending(value => value).ToArray(), similarities);
    }

    [Fact]
    public async Task CategoryFilter_RestrictsResultsToRequestedCategory()
    {
        var queryVector = await _embedder.EmbedAsync("Stand user protagonist");

        var results = await _search.SearchAsync(queryVector, "jojo", limit: 5, category: "character");

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.Equal("character", result.Category));
    }
}
