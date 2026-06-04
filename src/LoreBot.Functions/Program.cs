using LoreBot.Core.Abstractions;
using LoreBot.Core.Configuration;
using LoreBot.Core.GuardRails;
using LoreBot.Core.Middleware;
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
using Microsoft.Extensions.Logging;
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

        services.AddScoped<IRateLimitService>(sp =>
            new RateLimitService(sp.GetRequiredService<AppDbContext>(), 10, 50, 200));
        services.AddScoped<ICacheService, CacheService>();
        services.AddSingleton<InputGuardRails>();
        services.AddSingleton<OutputGuardRails>();

        services.AddScoped<IChatClient>(sp =>
        {
            var inner = openAiClient.GetChatClient("gpt-4o-mini").AsIChatClient();
            return inner.AsBuilder()
                .Use(next => new RateLimitingChatClient(
                    next, sp.GetRequiredService<IRateLimitService>(), () => "global"))
                .Use(next => new GuardRailsChatClient(
                    next,
                    sp.GetRequiredService<InputGuardRails>(),
                    sp.GetRequiredService<OutputGuardRails>(),
                    sp.GetRequiredService<ILogger<GuardRailsChatClient>>()))
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Build();
        });

        services.AddScoped<IChatService>(sp => new ChatService(
            sp.GetRequiredService<IEmbeddingService>(),
            sp.GetRequiredService<IVectorSearchService>(),
            sp.GetRequiredService<IChatClient>(),
            "JoJo's Bizarre Adventure",
            sp.GetRequiredService<ICacheService>()));
    })
    .Build();

host.Run();
