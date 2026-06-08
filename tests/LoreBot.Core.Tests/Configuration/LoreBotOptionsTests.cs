using LoreBot.Core.Configuration;
using Xunit;

namespace LoreBot.Core.Tests.Configuration;

public class LoreBotOptionsTests
{
    [Fact]
    public void Defaults_UseOpenAiAndPostgresProviders()
    {
        var options = new LoreBotOptions();

        Assert.Equal("openai", options.EmbeddingProvider);
        Assert.Equal("postgres", options.VectorStoreProvider);
        Assert.Equal("openai", options.LlmProvider);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_LocalEmbeddingWithoutModelPath_ReturnsError()
    {
        var options = new LoreBotOptions
        {
            EmbeddingProvider = "local"
        };

        var errors = options.Validate();

        Assert.Contains(errors, error => error.Contains("LOCAL_EMBEDDING_MODEL_PATH"));
    }

    [Fact]
    public void Validate_FileVectorStoreWithoutDataPath_ReturnsError()
    {
        var options = new LoreBotOptions
        {
            VectorStoreProvider = "file"
        };

        var errors = options.Validate();

        Assert.Contains(errors, error => error.Contains("RAG_DATA_PATH"));
    }

    [Fact]
    public void Validate_DeepSeekWithoutApiKey_ReturnsError()
    {
        var options = new LoreBotOptions
        {
            LlmProvider = "deepseek"
        };

        var errors = options.Validate();

        Assert.Contains(errors, error => error.Contains("DEEPSEEK_API_KEY"));
    }

    [Theory]
    [InlineData("ollama", "postgres", "openai", "EMBEDDING_PROVIDER")]
    [InlineData("openai", "memory", "openai", "VECTOR_STORE_PROVIDER")]
    [InlineData("openai", "postgres", "claude", "LLM_PROVIDER")]
    public void Validate_UnsupportedProvider_ReturnsError(
        string embeddingProvider,
        string vectorStoreProvider,
        string llmProvider,
        string expectedSetting)
    {
        var options = new LoreBotOptions
        {
            EmbeddingProvider = embeddingProvider,
            VectorStoreProvider = vectorStoreProvider,
            LlmProvider = llmProvider
        };

        var errors = options.Validate();

        Assert.Contains(errors, error => error.Contains(expectedSetting));
    }

    [Fact]
    public void Validate_ConfiguredLocalProfile_ReturnsNoErrors()
    {
        var options = new LoreBotOptions
        {
            EmbeddingProvider = "local",
            VectorStoreProvider = "file",
            LlmProvider = "deepseek",
            LocalEmbeddingModelPath = "/app/models/embedding.gguf",
            RagDataPath = "/app/rag-data",
            DeepSeekApiKey = "test-key"
        };

        Assert.Empty(options.Validate());
    }
}
