namespace LoreBot.Core.Configuration;

public class LoreBotOptions
{
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string DeepSeekApiKey { get; set; } = string.Empty;
    public string LlmProvider { get; set; } = "openai";
    public string EmbeddingProvider { get; set; } = "openai";
    public string VectorStoreProvider { get; set; } = "postgres";
    public string LocalEmbeddingModelPath { get; set; } = string.Empty;
    public string RagDataPath { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string DatabaseConnectionString { get; set; } = string.Empty;
    public string DefaultUniverseName { get; set; } = "JoJo's Bizarre Adventure";
    public string AdminApiKey { get; set; } = string.Empty;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!IsProvider(EmbeddingProvider, "openai", "local"))
        {
            errors.Add("EMBEDDING_PROVIDER must be either 'openai' or 'local'.");
        }

        if (!IsProvider(VectorStoreProvider, "postgres", "file"))
        {
            errors.Add("VECTOR_STORE_PROVIDER must be either 'postgres' or 'file'.");
        }

        if (!IsProvider(LlmProvider, "openai", "deepseek"))
        {
            errors.Add("LLM_PROVIDER must be either 'openai' or 'deepseek'.");
        }

        if (EmbeddingProvider.Equals("local", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(LocalEmbeddingModelPath))
        {
            errors.Add("LOCAL_EMBEDDING_MODEL_PATH is required when EMBEDDING_PROVIDER=local.");
        }

        if (VectorStoreProvider.Equals("file", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(RagDataPath))
        {
            errors.Add("RAG_DATA_PATH is required when VECTOR_STORE_PROVIDER=file.");
        }

        if (LlmProvider.Equals("deepseek", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(DeepSeekApiKey))
        {
            errors.Add("DEEPSEEK_API_KEY is required when LLM_PROVIDER=deepseek.");
        }

        return errors;
    }

    private static bool IsProvider(string value, params string[] supported) =>
        supported.Contains(value, StringComparer.OrdinalIgnoreCase);
}
