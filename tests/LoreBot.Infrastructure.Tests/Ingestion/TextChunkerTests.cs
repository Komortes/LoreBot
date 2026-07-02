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

    [Fact]
    public void Chunk_TextWithNoWhitespace_StillSplitsIntoMultipleChunks()
    {
        // CJK text (and base64 blobs, minified text, etc.) can contain no space
        // characters at all; the chunker must not collapse this into one giant chunk.
        var noSpaceText = string.Concat(Enumerable.Repeat("四条家的血脉传承", 200));
        var chunker = new TextChunker(maxTokens: 10, overlapTokens: 2);
        var chunks = chunker.Chunk(noSpaceText);
        Assert.True(chunks.Count > 1);
        for (int i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].Index);
    }

    [Fact]
    public void Chunk_TabsAndNewlines_AreTreatedAsWordSeparators()
    {
        var chunker = new TextChunker(maxTokens: 512, overlapTokens: 64);
        var chunks = chunker.Chunk("Dio\tBrando\nis a vampire.");
        Assert.Single(chunks);
        Assert.Equal("Dio Brando is a vampire.", chunks[0].Text);
    }
}
