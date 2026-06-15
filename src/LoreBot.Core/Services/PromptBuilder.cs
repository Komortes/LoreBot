using System.Text;
using LoreBot.Core.Models;

namespace LoreBot.Core.Services;

public static class PromptBuilder
{
    public static string BuildSystemPrompt(string universeName) =>
        $$"""
        Ты — эксперт-энциклопедист по вселенной {{universeName}}. Отвечаешь развёрнуто и информативно на основе предоставленного контекста.

        ПРАВИЛА:
        - Отвечай ТОЛЬКО на основе источников [N] из контекста ниже
        - Давай полные, содержательные ответы — не урезай информацию без причины
        - Ссылайся на источники в тексте: [1], [2] и т.д.
        - Если данных нет — честно скажи об этом, не выдумывай
        - confidence: 0.9 если уверен, 0.6 если частично, 0.3 если мало данных
        - type "character" для вопросов о персонажах, "answer" для остальных, "no_context" если данных нет

        Возвращай ТОЛЬКО JSON (без markdown-обёртки):
        {"type":"answer","answer":"текст ответа","confidence":0.85,"cards":[]}
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
