using LoreBot.Core.Models;
using Xunit;

namespace LoreBot.Core.Tests.Models;

public class ChatResultTests
{
    [Fact]
    public void ChatResult_DefaultSources_IsEmptyNotNull()
    {
        var result = new ChatResult { Answer = "hi", TokensUsed = 5 };
        Assert.NotNull(result.Sources);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public void Source_StoresTitleUrlCategory()
    {
        var s = new Source { Title = "Dio Brando", Url = "https://jojo.fandom.com/Dio", Category = "character" };
        Assert.Equal("Dio Brando", s.Title);
        Assert.Equal("character", s.Category);
    }
}
