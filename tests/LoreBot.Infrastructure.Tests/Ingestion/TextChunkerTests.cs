using LoreBot.Infrastructure.Ingestion;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Ingestion;

public class TextChunkerTests
{
    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var chunker = new TextChunker(maxTokens: 512, overlapTokens: 64);
        var chunks = chunker.Chunk("Dio Brando is a vampire.");
        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Index);
        Assert.Contains("Dio Brando", chunks[0].Text);
    }

    [Fact]
    public void Chunk_LongText_SplitsIntoMultipleOrderedChunks()
    {
        var paragraph = string.Join(". ", Enumerable.Range(0, 400).Select(i => $"Sentence number {i}"));
        var chunker = new TextChunker(maxTokens: 100, overlapTokens: 20);
        var chunks = chunker.Chunk(paragraph);
        Assert.True(chunks.Count > 1);
        for (int i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].Index);
    }

    [Fact]
    public void Chunk_EstimatesTokenCount()
    {
        var chunker = new TextChunker(maxTokens: 512, overlapTokens: 64);
        var chunks = chunker.Chunk("one two three four");
        Assert.True(chunks[0].TokenCount > 0);
    }
}
