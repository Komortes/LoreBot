namespace LoreBot.Core.Models;

public class RateLimit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Identifier { get; set; } = string.Empty;
    public string WindowType { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int TokenCount { get; set; }
    public DateTimeOffset WindowStart { get; set; }
}
