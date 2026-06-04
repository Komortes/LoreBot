namespace LoreBot.Core.Configuration;

public class LoreBotOptions
{
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string DatabaseConnectionString { get; set; } = string.Empty;
    public string DefaultUniverseName { get; set; } = "JoJo's Bizarre Adventure";
    public string AdminApiKey { get; set; } = string.Empty;
}
