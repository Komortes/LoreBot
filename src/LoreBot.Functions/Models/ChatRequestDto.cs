namespace LoreBot.Functions.Models;

public class ChatRequestDto
{
    public string Universe { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public List<ChatTurnDto> History { get; set; } = new();
}

public class ChatTurnDto
{
    public string Role { get; set; } = string.Empty;   // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
}
