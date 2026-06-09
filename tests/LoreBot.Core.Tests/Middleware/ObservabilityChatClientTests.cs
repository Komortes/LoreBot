using System.Diagnostics;
using LoreBot.Functions.Middleware;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

public class ObservabilityChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_CreatesSpanWithAttributes()
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == "LoreBot",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
             .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "answer")));

        var client = new ObservabilityChatClient(inner, "jojo");
        var messages = new List<ChatMessage> { new(ChatRole.User, "Кто такой Дио?") };
        await client.GetResponseAsync(messages, new ChatOptions { AdditionalProperties = new() { ["session_id"] = "s1" } });

        Assert.Single(spans);
        Assert.Equal("lorebot.chat.request", spans[0].DisplayName);
        Assert.Equal("jojo", spans[0].GetTagItem("universe.name"));
    }

    [Fact]
    public async Task GetResponseAsync_SetsErrorStatus_OnException()
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == "LoreBot",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
             .Returns<ChatResponse>(_ => throw new InvalidOperationException("LLM error"));

        var client = new ObservabilityChatClient(inner, "jojo");
        var messages = new List<ChatMessage> { new(ChatRole.User, "test") };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetResponseAsync(messages));

        Assert.Single(spans);
        Assert.Equal(ActivityStatusCode.Error, spans[0].Status);
    }
}
