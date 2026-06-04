namespace LoreBot.Functions.Models;

public class ChatRequestDto
{
    public string Universe { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}
