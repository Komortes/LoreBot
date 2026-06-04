namespace LoreBot.Core.Models;

public class Source
{
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Category { get; set; }
    public double Similarity { get; set; }
}
