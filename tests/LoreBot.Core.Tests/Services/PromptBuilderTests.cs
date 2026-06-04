using LoreBot.Core.Models;
using LoreBot.Core.Services;
using Xunit;

namespace LoreBot.Core.Tests.Services;

public class PromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_IncludesUniverseAndGroundingRule()
    {
        var prompt = PromptBuilder.BuildSystemPrompt("JoJo's Bizarre Adventure");
        Assert.Contains("JoJo's Bizarre Adventure", prompt);
        Assert.Contains("контекст", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildContextBlock_NumbersSourcesAndIncludesTitles()
    {
        var chunks = new List<RetrievedChunk>
        {
            new() { Title = "Dio Brando", Url = "https://jojo/Dio", ChunkText = "vampire", Category = "character" },
            new() { Title = "Stand", Url = "https://jojo/Stand", ChunkText = "ability", Category = "ability" },
        };
        var block = PromptBuilder.BuildContextBlock(chunks);
        Assert.Contains("[1]", block);
        Assert.Contains("[2]", block);
        Assert.Contains("Dio Brando", block);
        Assert.Contains("vampire", block);
    }
}
