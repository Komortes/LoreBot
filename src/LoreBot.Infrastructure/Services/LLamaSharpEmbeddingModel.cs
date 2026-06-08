using System.Diagnostics;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.Logging;

namespace LoreBot.Infrastructure.Services;

public sealed class LLamaSharpEmbeddingModel : ILocalEmbeddingModel
{
    private readonly LLamaWeights _weights;
    private readonly LLamaEmbedder _embedder;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LLamaSharpEmbeddingModel(string modelPath, ILogger<LLamaSharpEmbeddingModel> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var fullPath = Path.GetFullPath(modelPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Local embedding model file was not found.", fullPath);
        }

        var modelFile = new FileInfo(fullPath);
        var parameters = new ModelParams(fullPath)
        {
            Embeddings = true,
            PoolingType = LLamaPoolingType.Mean,
            Threads = Math.Max(1, Environment.ProcessorCount - 1)
        };

        var started = Stopwatch.StartNew();
        _weights = LLamaWeights.LoadFromFile(parameters);
        _embedder = new LLamaEmbedder(_weights, parameters, logger);
        started.Stop();

        logger.LogInformation(
            "Loaded local embedding model {ModelPath}. SizeBytes={ModelSizeBytes}, Dimension={Dimension}, LoadMilliseconds={LoadMilliseconds}, ManagedMemoryBytes={ManagedMemoryBytes}",
            fullPath,
            modelFile.Length,
            _embedder.EmbeddingSize,
            started.ElapsedMilliseconds,
            GC.GetTotalMemory(forceFullCollection: false));
    }

    public int EmbeddingSize => _embedder.EmbeddingSize;

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default) =>
        EmbedLockedAsync(text, ct);

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        var vectors = new float[texts.Count][];
        for (var index = 0; index < texts.Count; index++)
        {
            vectors[index] = await EmbedLockedAsync(texts[index], ct);
        }

        return vectors;
    }

    public void Dispose()
    {
        _embedder.Dispose();
        _weights.Dispose();
        _gate.Dispose();
    }

    private async Task<float[]> EmbedLockedAsync(string text, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var embeddings = await _embedder.GetEmbeddings(text, ct);
            if (embeddings.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Local embedding model returned {embeddings.Count} pooled vectors for one input.");
            }

            return embeddings[0];
        }
        finally
        {
            _gate.Release();
        }
    }
}
