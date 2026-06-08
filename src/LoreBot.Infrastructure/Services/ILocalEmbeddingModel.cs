namespace LoreBot.Infrastructure.Services;

public interface ILocalEmbeddingModel : IDisposable
{
    int EmbeddingSize { get; }

    Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default);

    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);
}
