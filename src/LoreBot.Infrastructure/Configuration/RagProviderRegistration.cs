using System.Security.Cryptography;
using LoreBot.Core.Abstractions;
using LoreBot.Core.Configuration;
using LoreBot.Core.Services;
using LoreBot.Infrastructure.Database.Repositories;
using LoreBot.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace LoreBot.Infrastructure.Configuration;

/// <summary>
/// Registers the embedding and vector-store implementations selected by <see cref="LoreBotOptions"/>.
/// Provider matrix:
/// <list type="bullet">
/// <item>openai-postgres: <see cref="OpenAiEmbeddingService"/> + <see cref="VectorSearchService"/></item>
/// <item>cheap-serverless: <see cref="LocalModelEmbeddingService"/> + <see cref="FileVectorSearchService"/></item>
/// <item>dev-local (openai selected, no key): <see cref="LocalEmbeddingService"/> hash projection</item>
/// </list>
/// The postgres store relies on <c>AppDbContext</c> being registered by the host.
/// </summary>
public static class RagProviderRegistration
{
    public static IServiceCollection AddRagProviders(this IServiceCollection services, LoreBotOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        AddEmbeddingProvider(services, options);
        AddVectorStoreProvider(services, options);
        return services;
    }

    private static void AddEmbeddingProvider(IServiceCollection services, LoreBotOptions options)
    {
        if (IsLocal(options.EmbeddingProvider))
        {
            // Real local model (e.g. a quantized gguf). Constructed lazily on first resolve so DI
            // registration never touches the large model file.
            services.AddSingleton<ILocalEmbeddingModel>(sp => new LLamaSharpEmbeddingModel(
                options.LocalEmbeddingModelPath,
                sp.GetRequiredService<ILogger<LLamaSharpEmbeddingModel>>()));
            services.AddSingleton<IEmbeddingService, LocalModelEmbeddingService>();
            return;
        }

        // OpenAI profile. With no API key we degrade to the deterministic hash-projection
        // embedder (dev-local profile) instead of failing, so local dev stays key-free.
        if (string.IsNullOrWhiteSpace(options.OpenAiApiKey))
        {
            services.AddSingleton<IEmbeddingService, LocalEmbeddingService>();
            return;
        }

        var apiKey = options.OpenAiApiKey;
        var embeddingModel = options.EmbeddingModel;
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            new OpenAIClient(apiKey).GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator());
        services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
    }

    private static void AddVectorStoreProvider(IServiceCollection services, LoreBotOptions options)
    {
        if (IsFile(options.VectorStoreProvider))
        {
            services.AddSingleton<IVectorSearchService>(sp => CreateFileVectorSearchService(sp, options));
            return;
        }

        services.AddScoped<IVectorSearchService, VectorSearchService>();
    }

    private static FileVectorSearchService CreateFileVectorSearchService(IServiceProvider sp, LoreBotOptions options)
    {
        // The file store validates the artifact against the embedding model that built it, so a file
        // store must pair with the local model to supply a meaningful hash and dimension.
        if (!IsLocal(options.EmbeddingProvider))
        {
            throw new InvalidOperationException(
                "VECTOR_STORE_PROVIDER=file requires EMBEDDING_PROVIDER=local so the artifact's "
                + "embedding model hash and dimension can be verified.");
        }

        var artifactPath = ResolveArtifactPath(options.RagDataPath);
        var modelHash = ComputeSha256(options.LocalEmbeddingModelPath);
        var dimension = sp.GetRequiredService<ILocalEmbeddingModel>().EmbeddingSize;
        return new FileVectorSearchService(artifactPath, modelHash, dimension);
    }

    /// <summary>
    /// Resolves <c>RAG_DATA_PATH</c> to a single artifact file. Accepts either a direct file path or a
    /// directory containing exactly one <c>lorebot-rag-index*.json</c> artifact.
    /// </summary>
    private static string ResolveArtifactPath(string ragDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ragDataPath);
        var fullPath = Path.GetFullPath(ragDataPath);

        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        if (Directory.Exists(fullPath))
        {
            var artifacts = Directory
                .EnumerateFiles(fullPath, "lorebot-rag-index*.json")
                .Where(path => !path.EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return artifacts.Length switch
            {
                1 => artifacts[0],
                0 => throw new FileNotFoundException(
                    $"No RAG index artifact (lorebot-rag-index*.json) was found in directory {fullPath}.",
                    fullPath),
                _ => throw new InvalidOperationException(
                    $"RAG_DATA_PATH directory {fullPath} contains multiple artifacts; "
                    + "point RAG_DATA_PATH at a single artifact file.")
            };
        }

        throw new FileNotFoundException($"RAG_DATA_PATH was not found: {fullPath}", fullPath);
    }

    private static string ComputeSha256(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static bool IsLocal(string provider) =>
        provider.Equals("local", StringComparison.OrdinalIgnoreCase);

    private static bool IsFile(string provider) =>
        provider.Equals("file", StringComparison.OrdinalIgnoreCase);
}
