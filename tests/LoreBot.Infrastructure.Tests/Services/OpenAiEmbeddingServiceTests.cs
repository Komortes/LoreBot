using LoreBot.Infrastructure.Services;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace LoreBot.Infrastructure.Tests.Services;

public class OpenAiEmbeddingServiceTests
{
    [Fact]
    public async Task EmbedAsync_ReturnsVectorFromGenerator()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var embedding = new Embedding<float>(new float[1536]);
        var result = new GeneratedEmbeddings<Embedding<float>>(new[] { embedding });
        generator.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        var svc = new OpenAiEmbeddingService(generator);
        var vec = await svc.EmbedAsync("Dio");

        Assert.Equal(1536, vec.Length);
    }

    [Fact]
    public async Task EmbedBatchAsync_ReturnsOneVectorPerInput()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var result = new GeneratedEmbeddings<Embedding<float>>(new[]
        {
            new Embedding<float>(new float[1536]),
            new Embedding<float>(new float[1536]),
        });
        generator.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        var svc = new OpenAiEmbeddingService(generator);
        var vecs = await svc.EmbedBatchAsync(new[] { "a", "b" });

        Assert.Equal(2, vecs.Count);
    }
}
