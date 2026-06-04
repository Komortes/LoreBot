namespace LoreBot.Core.Models;

public class ChatResult
{
    public string Answer { get; set; } = string.Empty;
    public List<Source> Sources { get; set; } = new();
    public int TokensUsed { get; set; }
    public bool FromCache { get; set; }
    public bool Blocked { get; set; }
}
