using LoreBot.Core.Models;
using Xunit;

namespace LoreBot.Evaluations.TestCases;

public class StructuredResponseContractTests
{
    [Fact]
    public async Task GroundedAnswer_HasValidStructuredContract()
    {
        var chat = TestInfrastructure.BuildDeterministicChatService();

        var result = await chat.ChatAsync("jojo", "Кто такой Дио?", "eval-contract");

        AssertValidContract(result);
        Assert.NotEmpty(result.Sources);
        Assert.InRange(result.Confidence ?? 0, 0, 1);
    }

    [Fact]
    public async Task NoContextAnswer_UsesNoContextTypeWithoutSources()
    {
        var chat = TestInfrastructure.BuildDeterministicChatService();

        var result = await chat.ChatAsync("jojo", "Кто такой неизвестный персонаж?", "eval-no-context");

        AssertValidContract(result);
        Assert.Equal("no_context", result.Type);
        Assert.Empty(result.Sources);
    }

    private static void AssertValidContract(ChatResult result)
    {
        Assert.False(string.IsNullOrWhiteSpace(result.Type));
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
        Assert.NotNull(result.Sources);
        Assert.NotNull(result.Cards);
    }
}
