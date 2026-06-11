using System.Security.Cryptography;
using System.Text.Json;
using LoreBot.Infrastructure.Database.Repositories;
using LoreBot.Infrastructure.Ingestion;
using LoreBot.Infrastructure.Models;
using LoreBot.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        PrintUsage();
        return args.Length == 0 ? 1 : 0;
    }

    try
    {
        return args[0].ToLowerInvariant() switch
        {
            "build" => await BuildAsync(CliOptions.Parse(args[1..])),
            "verify" => await VerifyAsync(CliOptions.Parse(args[1..])),
            _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
        };
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"error: {exception.Message}");
        return 1;
    }
}

static async Task<int> BuildAsync(CliOptions options)
{
    var universe = options.RequireSingle("universe");
    var inputDirectory = Path.GetFullPath(options.RequireSingle("input"));
    var outputDirectory = Path.GetFullPath(options.RequireSingle("output"));
    var modelPath = Path.GetFullPath(options.RequireSingle("model"));

    if (!Directory.Exists(inputDirectory))
    {
        throw new DirectoryNotFoundException($"Input directory was not found: {inputDirectory}");
    }

    var documents = LoadDocuments(inputDirectory, universe);
    if (documents.Count == 0)
    {
        throw new InvalidOperationException(
            $"No .md or .txt files were found under {inputDirectory}.");
    }

    var modelHash = await ComputeSha256Async(modelPath);
    var embeddingModel = new LLamaSharpEmbeddingModel(
        modelPath,
        NullLogger<LLamaSharpEmbeddingModel>.Instance);
    using var embeddingService = new LocalModelEmbeddingService(
        embeddingModel,
        NullLogger<LocalModelEmbeddingService>.Instance);
    var builder = new RagArtifactBuilder(
        embeddingService,
        new TextChunker(maxTokens: 400, overlapTokens: 50));

    Console.WriteLine(
        $"Indexing {documents.Count} source document(s) for '{universe}'...");
    var artifact = await builder.BuildAsync(documents, modelHash);
    var result = await builder.WriteAsync(artifact, outputDirectory, universe);

    Console.WriteLine($"Artifact: {result.ArtifactPath}");
    Console.WriteLine($"Metadata: {result.MetadataPath}");
    Console.WriteLine(
        $"Chunks: {artifact.Entries.Count}; dimension: {artifact.VectorDimension}; size: {result.ArtifactSizeBytes} bytes");
    return 0;
}

static async Task<int> VerifyAsync(CliOptions options)
{
    var artifactPath = Path.GetFullPath(options.RequireSingle("artifact"));
    var modelPath = Path.GetFullPath(options.RequireSingle("model"));
    var queries = options.GetMany("query");

    if (queries.Count == 0)
    {
        throw new ArgumentException("At least one --query is required for verification.");
    }

    var artifact = await ReadArtifactAsync(artifactPath);
    var modelHash = await ComputeSha256Async(modelPath);
    var embeddingModel = new LLamaSharpEmbeddingModel(
        modelPath,
        NullLogger<LLamaSharpEmbeddingModel>.Instance);
    using var embeddingService = new LocalModelEmbeddingService(
        embeddingModel,
        NullLogger<LocalModelEmbeddingService>.Instance);
    var search = new FileVectorSearchService(
        artifactPath,
        modelHash,
        embeddingModel.EmbeddingSize);

    foreach (var query in queries)
    {
        var universe = options.GetSingle("universe")
            ?? artifact.Entries.FirstOrDefault()?.UniverseSlug
            ?? throw new InvalidDataException(
                "Empty artifacts require an explicit --universe option.");
        var vector = await embeddingService.EmbedAsync(query);
        var matches = await search.SearchAsync(
            vector,
            universe,
            limit: 3);

        Console.WriteLine($"Query: {query}");
        foreach (var match in matches)
        {
            Console.WriteLine($"  {match.Similarity:F4}  {match.Title}");
        }
    }

    Console.WriteLine(
        $"Verified schema {artifact.SchemaVersion}, {artifact.Entries.Count} chunks, dimension {artifact.VectorDimension}.");
    return 0;
}

static IReadOnlyList<RagSourceDocument> LoadDocuments(
    string inputDirectory,
    string universeSlug)
{
    var supportedExtensions = new HashSet<string>(
        [".md", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    return Directory
        .EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
        .Where(path => supportedExtensions.Contains(Path.GetExtension(path)))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .Select(path =>
        {
            var relativePath = Path.GetRelativePath(inputDirectory, path);
            var segments = relativePath.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);
            var category = segments.Length > 1 ? segments[0] : null;

            return new RagSourceDocument(
                universeSlug,
                Path.GetFileNameWithoutExtension(path),
                Url: null,
                Category: category,
                Text: File.ReadAllText(path));
        })
        .ToArray();
}

static async Task<RagIndexArtifact> ReadArtifactAsync(string artifactPath)
{
    await using var stream = File.OpenRead(artifactPath);
    return await JsonSerializer.DeserializeAsync<RagIndexArtifact>(stream)
        ?? throw new InvalidDataException("RAG index artifact is empty.");
}

static async Task<string> ComputeSha256Async(string path)
{
    await using var stream = File.OpenRead(path);
    var hash = await SHA256.HashDataAsync(stream);
    return Convert.ToHexStringLower(hash);
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        LoreBot offline RAG indexer

        Build:
          dotnet run --project src/LoreBot.Indexer -- build \
            --universe jojo --input ./input-data/jojo --output ./rag-data \
            --model ./models/embedding.gguf

        Verify:
          dotnet run --project src/LoreBot.Indexer -- verify \
            --artifact ./rag-data/lorebot-rag-index.jojo.json \
            --model ./models/embedding.gguf --universe jojo \
            --query "Who is Dio Brando?" --query "What can The World do?"
        """);
}

file sealed class CliOptions
{
    private readonly Dictionary<string, List<string>> _values;

    private CliOptions(Dictionary<string, List<string>> values) =>
        _values = values;

    public static CliOptions Parse(string[] args)
    {
        var values = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal)
                || index + 1 >= args.Length)
            {
                throw new ArgumentException(
                    $"Expected '--name value' at argument {index + 1}.");
            }

            var name = args[index][2..];
            if (!values.TryGetValue(name, out var optionValues))
            {
                optionValues = [];
                values[name] = optionValues;
            }

            optionValues.Add(args[index + 1]);
        }

        return new CliOptions(values);
    }

    public string RequireSingle(string name) =>
        GetSingle(name)
        ?? throw new ArgumentException($"Missing required option --{name}.");

    public string? GetSingle(string name) =>
        _values.TryGetValue(name, out var values) ? values.LastOrDefault() : null;

    public IReadOnlyList<string> GetMany(string name) =>
        _values.TryGetValue(name, out var values) ? values : [];
}
