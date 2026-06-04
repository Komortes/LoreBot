using LoreBot.Core.Abstractions;
using LoreBot.Core.Configuration;
using LoreBot.Core.Services;
using LoreBot.Infrastructure.Database;
using LoreBot.Infrastructure.Database.Repositories;
using LoreBot.Infrastructure.Ingestion;
using LoreBot.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.Configure<LoreBotOptions>(o =>
        {
            o.OpenAiApiKey = config["OPENAI_API_KEY"] ?? "";
            o.DatabaseConnectionString = config["DATABASE_CONNECTION_STRING"] ?? "";
            o.AdminApiKey = config["ADMIN_API_KEY"] ?? "";
        });

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(config["DATABASE_CONNECTION_STRING"] ?? "", n => n.UseVector()));

        var openAiClient = new OpenAIClient(config["OPENAI_API_KEY"] ?? "");

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            openAiClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator());

        services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
        services.AddScoped<IVectorSearchService, VectorSearchService>();
        services.AddScoped<TextChunker>(_ => new TextChunker(512, 64));
        services.AddScoped<IndexingPipeline>();
        services.AddHttpClient<WikiScraper>();

        services.AddSingleton<IChatClient>(_ =>
            openAiClient.GetChatClient("gpt-4o-mini").AsIChatClient()
                .AsBuilder()
                .Build());

        services.AddScoped<IChatService>(sp => new ChatService(
            sp.GetRequiredService<IEmbeddingService>(),
            sp.GetRequiredService<IVectorSearchService>(),
            sp.GetRequiredService<IChatClient>(),
            "JoJo's Bizarre Adventure"));
    })
    .Build();

host.Run();
