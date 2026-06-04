using LoreBot.Core.Abstractions;
using LoreBot.Core.Middleware;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace LoreBot.Core.Tests.Middleware;

public class RateLimitingChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_WhenBlocked_ReturnsDegradedMessage()
    {
        var inner = Substitute.For<IChatClient>();
        var limiter = Substitute.For<IRateLimitService>();
        limiter.CheckAndIncrementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RateLimitDecision(false, "Rate limit exceeded for window: minute")));

        var client = new RateLimitingChatClient(inner, limiter, () => "1.2.3.4");
        var response = await client.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        Assert.Contains("перегружен", response.Text, StringComparison.OrdinalIgnoreCase);
        await inner.DidNotReceive().GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponseAsync_WhenAllowed_CallsInner()
    {
        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));
        var limiter = Substitute.For<IRateLimitService>();
        limiter.CheckAndIncrementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RateLimitDecision(true, null)));

        var client = new RateLimitingChatClient(inner, limiter, () => "1.2.3.4");
        var response = await client.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        Assert.Equal("ok", response.Text);
    }
}
