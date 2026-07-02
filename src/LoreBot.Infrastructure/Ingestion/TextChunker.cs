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

    // A hard ceiling on how long a single whitespace-delimited "word" can be before the
    // chunker forces a split. Ordinary text never hits this; it exists so that text with
    // no space characters at all (CJK/Korean prose, minified text, a base64 blob, one
    // giant line) can't collapse the whole document into a single unbounded "word" that
    // bypasses the max-token limit.
    private const int MaxWordChars = 40;

    private static int EstimateTokens(string text) =>
        (int)Math.Ceiling(Tokenize(text).Length / 0.75);

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        var words = Tokenize(text);
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

    // Splits on any Unicode whitespace (not just the literal space character), then breaks
    // up any resulting token longer than MaxWordChars into fixed-size pieces.
    private static string[] Tokenize(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.All(w => w.Length <= MaxWordChars)) return words;

        var expanded = new List<string>(words.Length);
        foreach (var word in words)
        {
            if (word.Length <= MaxWordChars)
            {
                expanded.Add(word);
                continue;
            }
            for (int i = 0; i < word.Length; i += MaxWordChars)
                expanded.Add(word.Substring(i, Math.Min(MaxWordChars, word.Length - i)));
        }
        return expanded.ToArray();
    }
}
