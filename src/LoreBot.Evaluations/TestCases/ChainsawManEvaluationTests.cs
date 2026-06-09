using Xunit;

namespace LoreBot.Evaluations.TestCases;

public class ChainsawManEvaluationTests
{
    [Fact]
    public async Task Denji_AnswerContainsKeyConceptsAndSources()
    {
        var chat = TestInfrastructure.BuildDeterministicChatService();

        var result = await chat.ChatAsync("chainsaw_man", "Кто такой Дэндзи?", "eval-cm-denji");

        Assert.Equal("character", result.Type);
        Assert.True(
            result.Answer.Contains("chainsaw", StringComparison.OrdinalIgnoreCase)
            || result.Answer.Contains("бензопил", StringComparison.OrdinalIgnoreCase)
            || result.Answer.Contains("devil", StringComparison.OrdinalIgnoreCase),
            $"Expected chainsaw/devil concept. Got: {result.Answer}");
        Assert.NotEmpty(result.Sources);
        Assert.All(result.Sources, s => Assert.False(string.IsNullOrWhiteSpace(s.Title)));
    }
}
