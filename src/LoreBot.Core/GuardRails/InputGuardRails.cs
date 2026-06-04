namespace LoreBot.Core.GuardRails;

public class InputGuardRails
{
    private static readonly string[] JailbreakPatterns =
    {
        "ignore previous instructions",
        "ignore all previous",
        "you are now",
        "forget your system prompt",
        "act as if",
        "pretend you are",
        "disregard the above",
        "игнорируй предыдущие инструкции",
        "забудь свои инструкции",
        "ты теперь",
    };

    private static readonly string[] NsfwKeywords =
    {
        "nsfw", "explicit sexual", "porn",
    };

    public bool IsJailbreakAttempt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return JailbreakPatterns.Any(p => lower.Contains(p));
    }

    public bool IsNsfw(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return NsfwKeywords.Any(k => lower.Contains(k));
    }
}
