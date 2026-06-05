namespace LoreBot.Core.Models;

public class ChatResult
{
    public string Type { get; set; } = "answer";
    public string Answer { get; set; } = string.Empty;
    public List<Source> Sources { get; set; } = new();
    public double? Confidence { get; set; }
    public List<ChatCard> Cards { get; set; } = new();
    public int TokensUsed { get; set; }
    public bool FromCache { get; set; }
    public bool Blocked { get; set; }
}

public class ChatCard
{
    public string Type { get; set; } = "info";
    public string? Title { get; set; }
    public string? Body { get; set; }
}
