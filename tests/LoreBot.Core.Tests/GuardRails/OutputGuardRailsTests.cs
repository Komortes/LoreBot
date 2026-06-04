using LoreBot.Core.GuardRails;
using Xunit;

namespace LoreBot.Core.Tests.GuardRails;

public class OutputGuardRailsTests
{
    private readonly OutputGuardRails _guard = new();

    [Theory]
    [InlineData("As a language model, I cannot...")]
    [InlineData("Как языковая модель, я не могу")]
    public void IsModelDisclaimer_DetectsBoilerplate(string answer)
    {
        Assert.True(_guard.IsModelDisclaimer(answer));
    }

    [Fact]
    public void HasCitation_DetectsBracketReference()
    {
        Assert.True(_guard.HasCitation("Дио — вампир [1]."));
        Assert.False(_guard.HasCitation("Дио — вампир."));
    }

    [Fact]
    public void IsGrounded_TrueWhenAnswerSharesTokensWithContext()
    {
        var context = "Dio Brando became a vampire using the Stone Mask.";
        Assert.True(_guard.IsGrounded("Dio became a vampire.", context));
        Assert.False(_guard.IsGrounded("Naruto uses chakra to fight Sasuke.", context));
    }
}
