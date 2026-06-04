namespace LoreBot.Core.Models;

public class EvaluationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? SessionId { get; set; }
    public Guid UniverseId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? RetrievedChunksJson { get; set; }
    public double? GroundednessScore { get; set; }
    public double? RelevanceScore { get; set; }
    public string? GuardrailFlagsJson { get; set; }
    public int TokensUsed { get; set; }
    public int LatencyMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
