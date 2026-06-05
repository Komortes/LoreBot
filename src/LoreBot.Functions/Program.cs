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
            o.DeepSeekApiKey = config["DEEPSEEK_API_KEY"] ?? "";
            o.LlmProvider = config["LLM_PROVIDER"] ?? "openai";
            o.DatabaseConnectionString = config["DATABASE_CONNECTION_STRING"] ?? "";
            o.AdminApiKey = config["ADMIN_API_KEY"] ?? "";
        });

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(config["DATABASE_CONNECTION_STRING"] ?? "", n => n.UseVector()));

        // Embeddings: use OpenAI if key is set, otherwise fall back to local hash-projection (dev only)
        var openAiApiKey = config["OPENAI_API_KEY"] is { Length: > 0 } k ? k : null;
        var openAiClient = openAiApiKey is not null ? new OpenAIClient(openAiApiKey) : null;
        if (openAiClient is not null)
        {
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
                openAiClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator());
            services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
        }
        else
        {
            services.AddSingleton<IEmbeddingService, LocalEmbeddingService>();
        }

        // Chat client: DeepSeek or OpenAI based on LLM_PROVIDER
        var llmProvider = config["LLM_PROVIDER"] ?? "openai";
        var deepSeekKey = config["DEEPSEEK_API_KEY"] is { Length: > 0 } dk ? dk : null;
        OpenAIClient chatOpenAiClient;
        string chatModelName;
        if (llmProvider.Equals("deepseek", StringComparison.OrdinalIgnoreCase) && deepSeekKey is not null)
        {
            chatOpenAiClient = new OpenAIClient(
                new System.ClientModel.ApiKeyCredential(deepSeekKey),
                new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.deepseek.com/v1") });
            chatModelName = "deepseek-chat";
        }
        else
        {
            chatOpenAiClient = openAiClient ?? new OpenAIClient("sk-placeholder");
            chatModelName = "gpt-4o-mini";
        }

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
            var inner = chatOpenAiClient.GetChatClient(chatModelName).AsIChatClient();
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
