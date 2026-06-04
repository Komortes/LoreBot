using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Core.Services;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace LoreBot.Core.Tests.Services;

public class ChatServiceTests
{
    private static (ChatService svc, IChatClient chat) Build(string answer, IReadOnlyList<RetrievedChunk> chunks)
    {
        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[1536]));

        var search = Substitute.For<IVectorSearchService>();
        search.SearchAsync(Arg.Any<float[]>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chunks));

        var chat = Substitute.For<IChatClient>();
        chat.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer))
            {
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 }
            }));

        return (new ChatService(embedder, search, chat, "JoJo"), chat);
    }

    [Fact]
    public async Task ChatAsync_ReturnsAnswerAndSources()
    {
        var chunks = new List<RetrievedChunk>
        {
            new() { Title = "Dio Brando", Url = "https://jojo/Dio", ChunkText = "vampire", Category = "character", Similarity = 0.9 }
        };
        var (svc, _) = Build("Дио Брандо — вампир [1].", chunks);
        var result = await svc.ChatAsync("jojo", "Кто такой Дио?", "session-1");

        Assert.Contains("вампир", result.Answer);
        Assert.Single(result.Sources);
        Assert.Equal("Dio Brando", result.Sources[0].Title);
        Assert.Equal(150, result.TokensUsed);
    }

    [Fact]
    public async Task ChatAsync_NoChunks_ReturnsNoInfoAnswerWithoutCallingLlm()
    {
        var (svc, chat) = Build("unused", new List<RetrievedChunk>());
        var result = await svc.ChatAsync("jojo", "Кто такой Икс?", "session-2");

        Assert.Empty(result.Sources);
        await chat.DidNotReceive().GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }
}
