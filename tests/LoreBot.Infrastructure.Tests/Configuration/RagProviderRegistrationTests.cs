using LoreBot.Core.Abstractions;
using LoreBot.Core.Configuration;
using LoreBot.Core.Services;
using LoreBot.Infrastructure.Configuration;
using LoreBot.Infrastructure.Database.Repositories;
using LoreBot.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Configuration;

public class RagProviderRegistrationTests
{
    private static ServiceDescriptor Descriptor(IServiceCollection services, Type serviceType) =>
        services.Single(d => d.ServiceType == serviceType);

    [Fact]
    public void OpenAiPostgresProfile_RegistersOpenAiEmbeddingAndPgVectorSearch()
    {
        var services = new ServiceCollection();
        var options = new LoreBotOptions
        {
            EmbeddingProvider = "openai",
            VectorStoreProvider = "postgres",
            OpenAiApiKey = "sk-test"
        };

        services.AddRagProviders(options);

        Assert.Equal(typeof(OpenAiEmbeddingService), Descriptor(services, typeof(IEmbeddingService)).ImplementationType);
        Assert.Equal(typeof(VectorSearchService), Descriptor(services, typeof(IVectorSearchService)).ImplementationType);
    }

    [Fact]
    public void OpenAiProfileWithoutKey_FallsBackToHashProjectionEmbedding()
    {
        var services = new ServiceCollection();
        var options = new LoreBotOptions
        {
            EmbeddingProvider = "openai",
            VectorStoreProvider = "postgres",
            OpenAiApiKey = ""
        };

        services.AddRagProviders(options);

        Assert.Equal(typeof(LocalEmbeddingService), Descriptor(services, typeof(IEmbeddingService)).ImplementationType);
    }

    [Fact]
    public void CheapServerlessProfile_RegistersLocalModelEmbeddingAndFileVectorSearch()
    {
        var services = new ServiceCollection();
        var options = new LoreBotOptions
        {
            EmbeddingProvider = "local",
            VectorStoreProvider = "file",
            LlmProvider = "deepseek",
            LocalEmbeddingModelPath = "/app/models/embedding.gguf",
            RagDataPath = "/app/rag-data/lorebot-rag-index.jojo.json",
            DeepSeekApiKey = "test-key"
        };

        services.AddRagProviders(options);

        var embedding = Descriptor(services, typeof(IEmbeddingService));
        Assert.Equal(typeof(LocalModelEmbeddingService), embedding.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, embedding.Lifetime);

        // The real gguf model is constructed lazily through a factory so DI registration
        // never touches the (large) model file.
        var model = Descriptor(services, typeof(ILocalEmbeddingModel));
        Assert.NotNull(model.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Singleton, model.Lifetime);

        var vectorSearch = Descriptor(services, typeof(IVectorSearchService));
        Assert.NotNull(vectorSearch.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Singleton, vectorSearch.Lifetime);
    }
}
