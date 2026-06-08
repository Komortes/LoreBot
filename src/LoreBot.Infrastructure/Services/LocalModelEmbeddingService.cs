using LoreBot.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace LoreBot.Infrastructure.Services;

public sealed class LocalModelEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly ILocalEmbeddingModel _model;
    private readonly ILogger<LocalModelEmbeddingService> _logger;

    public LocalModelEmbeddingService(
        ILocalEmbeddingModel model,
        ILogger<LocalModelEmbeddingService> logger)
    {
        _model = model;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var vector = await _model.EmbedQueryAsync(text, ct);
        return NormalizeAndValidate(vector);
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        if (texts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Embedding input cannot contain empty text.", nameof(texts));
        }

        var vectors = await _model.EmbedDocumentsAsync(texts, ct);
        if (vectors.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Local embedding model returned {vectors.Count} vectors for {texts.Count} inputs.");
        }

        return vectors.Select(NormalizeAndValidate).ToArray();
    }

    public void Dispose() => _model.Dispose();

    private float[] NormalizeAndValidate(float[] vector)
    {
        if (vector.Length != _model.EmbeddingSize)
        {
            throw new InvalidOperationException(
                $"Local embedding model returned dimension {vector.Length}; expected {_model.EmbeddingSize}.");
        }

        var magnitude = MathF.Sqrt(vector.Sum(value => value * value));
        if (!float.IsFinite(magnitude) || magnitude <= 0)
        {
            throw new InvalidOperationException("Local embedding model returned an invalid zero-length vector.");
        }

        var normalized = new float[vector.Length];
        for (var index = 0; index < vector.Length; index++)
        {
            normalized[index] = vector[index] / magnitude;
        }

        _logger.LogDebug("Generated normalized local embedding with dimension {Dimension}.", normalized.Length);
        return normalized;
    }
}
