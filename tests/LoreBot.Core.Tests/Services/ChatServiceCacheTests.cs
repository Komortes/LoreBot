using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;
using LoreBot.Core.Services;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace LoreBot.Core.Tests.Services;

public class ChatServiceCacheTests
{
    [Fact]
    public async Task ChatAsync_CacheHit_SkipsSearchAndLlm()
    {
        var embedder = Substitute.For<IEmbeddingService>();
        embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[1536]));

        var search = Substitute.For<IVectorSearchService>();
        var chat = Substitute.For<IChatClient>();
        var cache = Substitute.For<ICacheService>();
        cache.TryGetAsync(Arg.Any<string>(), Arg.Any<float[]>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChatResult?>(new ChatResult { Answer = "cached", FromCache = true }));

        var svc = new ChatService(embedder, search, chat, "JoJo", cache);
        var result = await svc.ChatAsync("jojo", "q", "s");

        Assert.True(result.FromCache);
        Assert.Equal("cached", result.Answer);
        await search.DidNotReceive().SearchAsync(Arg.Any<float[]>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
