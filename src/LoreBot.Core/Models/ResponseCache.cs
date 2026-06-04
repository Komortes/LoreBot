namespace LoreBot.Core.Models;

public class ResponseCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniverseId { get; set; }
    public string QuestionHash { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public float[]? QuestionVector { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public string? SourcesJson { get; set; }
    public int HitCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
