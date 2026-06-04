using System.Text.RegularExpressions;

namespace LoreBot.Core.GuardRails;

public class OutputGuardRails
{
    private static readonly string[] DisclaimerPatterns =
    {
        "as a language model",
        "as an ai language model",
        "as an ai model",
        "как языковая модель",
        "как искусственный интеллект",
    };

    public bool IsModelDisclaimer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        var lower = answer.ToLowerInvariant();
        return DisclaimerPatterns.Any(p => lower.Contains(p));
    }

    public bool HasCitation(string? answer)
        => !string.IsNullOrWhiteSpace(answer) && Regex.IsMatch(answer, @"\[\d+\]");

    public bool IsGrounded(string answer, string context, double threshold = 0.3)
    {
        var ctxTokens = Tokenize(context).ToHashSet();
        var ansTokens = Tokenize(answer).ToList();
        if (ansTokens.Count == 0) return false;
        var overlap = ansTokens.Count(t => ctxTokens.Contains(t));
        return (double)overlap / ansTokens.Count >= threshold;
    }

    private static IEnumerable<string> Tokenize(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"\p{L}{4,}").Select(m => m.Value);
}
