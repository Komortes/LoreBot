namespace LoreBot.Infrastructure.Ingestion;

public record TextChunk(int Index, string Text, int TokenCount);

public class TextChunker
{
    private readonly int _maxTokens;
    private readonly int _overlapTokens;

    public TextChunker(int maxTokens = 512, int overlapTokens = 64)
    {
        _maxTokens = maxTokens;
        _overlapTokens = overlapTokens;
    }

    private static int EstimateTokens(string text) =>
        (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length / 0.75);

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return Array.Empty<TextChunk>();

        int wordsPerChunk = Math.Max(1, (int)(_maxTokens * 0.75));
        int overlapWords = Math.Max(0, (int)(_overlapTokens * 0.75));
        int step = Math.Max(1, wordsPerChunk - overlapWords);

        var chunks = new List<TextChunk>();
        int index = 0;
        for (int start = 0; start < words.Length; start += step)
        {
            var slice = words.Skip(start).Take(wordsPerChunk).ToArray();
            if (slice.Length == 0) break;
            var chunkText = string.Join(' ', slice);
            chunks.Add(new TextChunk(index++, chunkText, EstimateTokens(chunkText)));
            if (start + wordsPerChunk >= words.Length) break;
        }
        return chunks;
    }
}
