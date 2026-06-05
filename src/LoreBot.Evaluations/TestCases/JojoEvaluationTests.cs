using Xunit;

namespace LoreBot.Evaluations.TestCases;

public class JojoEvaluationTests
{
    [Fact]
    public async Task DioBrando_AnswerContainsKeyConceptsAndSources()
    {
        var chat = TestInfrastructure.BuildDeterministicChatService();

        var result = await chat.ChatAsync("jojo", "Кто такой Дио?", "eval-jojo-dio");

        Assert.Equal("character", result.Type);
        Assert.Contains("Дио", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("вампир", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Sources);
        Assert.All(result.Sources, source => Assert.False(string.IsNullOrWhiteSpace(source.Title)));
    }

    [Fact]
    public async Task Stand_AnswerExplainsAbilityConceptWithSources()
    {
        var chat = TestInfrastructure.BuildDeterministicChatService();

        var result = await chat.ChatAsync("jojo", "Что такое стенд?", "eval-jojo-stand");

        Assert.Contains("способность", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Sources);
        Assert.Contains(result.Sources, source => source.Category == "ability");
    }
}
