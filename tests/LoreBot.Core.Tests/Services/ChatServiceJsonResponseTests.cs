using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Core.Services;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace LoreBot.Core.Tests.Services;

public class ChatServiceJsonResponseTests
{
    [Fact]
    public async Task ChatAsync_ParsesStructuredJsonResponse()
    {
        var json = """
                   {
                     "type": "character",
                     "answer": "Дио Брандо — вампир и ключевой антагонист [1].",
                     "confidence": 0.87,
                     "cards": [
                       { "type": "character", "title": "Dio Brando", "body": "Vampire antagonist" }
                     ]
                   }
                   """;
        var svc = Build(json);

        var result = await svc.ChatAsync("jojo", "Кто такой Дио?", "session-1");

        Assert.Equal("character", result.Type);
        Assert.Contains("Дио Брандо", result.Answer);
        Assert.Equal(0.87, result.Confidence);
        Assert.Single(result.Cards);
        Assert.Equal("Dio Brando", result.Cards[0].Title);
    }

    [Fact]
    public async Task ChatAsync_InvalidJsonResponse_FallsBackToPlainAnswer()
    {
        var svc = Build("Дио Брандо — вампир [1].");

        var result = await svc.ChatAsync("jojo", "Кто такой Дио?", "session-1");

        Assert.Equal("answer", result.Type);
        Assert.Equal("Дио Брандо — вампир [1].", result.Answer);
        Assert.Null(result.Confidence);
        Assert.Empty(result.Cards);
    }

    private static ChatService Build(string answer)
    {
        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[1536]));

        var search = Substitute.For<IVectorSearchService>();
        search.SearchAsync(Arg.Any<float[]>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RetrievedChunk>>([
                new RetrievedChunk
                {
                    Title = "Dio Brando",
                    Url = "https://jojo/Dio",
                    ChunkText = "Dio Brando is a vampire.",
                    Category = "character",
                    Similarity = 0.9
                }
            ]));

        var chat = Substitute.For<IChatClient>();
        chat.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer))));

        return new ChatService(embedder, search, chat, "JoJo");
    }
}
