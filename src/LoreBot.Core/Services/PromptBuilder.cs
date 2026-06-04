using System.Text;
using LoreBot.Core.Models;

namespace LoreBot.Core.Services;

public static class PromptBuilder
{
    public static string BuildSystemPrompt(string universeName) =>
        $"""
        Ты эксперт по вселенной {universeName}.
        Отвечай ТОЛЬКО на основе предоставленного ниже контекста.
        Если в контексте нет ответа — честно скажи, что информации недостаточно.
        Всегда указывай источник цитаты в формате [N], где N — номер источника.
        Не выдумывай факты и не используй знания за пределами контекста.
        """;

    public static string BuildContextBlock(IReadOnlyList<RetrievedChunk> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Контекст (источники):");
        for (int i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            sb.AppendLine($"[{i + 1}] {c.Title} ({c.Url})");
            sb.AppendLine(c.ChunkText);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
