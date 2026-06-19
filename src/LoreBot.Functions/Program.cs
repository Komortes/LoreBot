using LoreBot.Core.Abstractions;
using LoreBot.Core.Configuration;
using LoreBot.Core.GuardRails;
using LoreBot.Core.Middleware;
using LoreBot.Core.Services;
using LoreBot.Core.Tools;
using LoreBot.Functions.Middleware;
using LoreBot.Infrastructure.Configuration;
using LoreBot.Infrastructure.Database;
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
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        // Bind + validate provider options for IOptions<LoreBotOptions> consumers (e.g. AdminFunction).
        services.AddOptions<LoreBotOptions>()
            .Configure(o =>
            {
                o.OpenAiApiKey = config["OPENAI_API_KEY"] ?? "";
                o.DeepSeekApiKey = config["DEEPSEEK_API_KEY"] ?? "";
                o.LlmProvider = config["LLM_PROVIDER"] ?? "openai";
                o.EmbeddingProvider = config["EMBEDDING_PROVIDER"] ?? "openai";
                o.VectorStoreProvider = config["VECTOR_STORE_PROVIDER"] ?? "postgres";
                o.LocalEmbeddingModelPath = config["LOCAL_EMBEDDING_MODEL_PATH"] ?? "";
                o.RagDataPath = config["RAG_DATA_PATH"] ?? "";
                o.DatabaseConnectionString = config["DATABASE_CONNECTION_STRING"] ?? "";
                o.AdminApiKey = config["ADMIN_API_KEY"] ?? "";
            })
            .Validate(
                options => options.Validate().Count == 0,
                "Invalid LoreBot provider configuration. Check provider-specific environment variables.")
            .ValidateOnStart();

        // Same values, materialized now to select concrete providers during composition.
        var loreBotOptions = new LoreBotOptions
        {
            OpenAiApiKey = config["OPENAI_API_KEY"] ?? "",
            DeepSeekApiKey = config["DEEPSEEK_API_KEY"] ?? "",
            LlmProvider = config["LLM_PROVIDER"] ?? "openai",
            EmbeddingProvider = config["EMBEDDING_PROVIDER"] ?? "openai",
            VectorStoreProvider = config["VECTOR_STORE_PROVIDER"] ?? "postgres",
            LocalEmbeddingModelPath = config["LOCAL_EMBEDDING_MODEL_PATH"] ?? "",
            RagDataPath = config["RAG_DATA_PATH"] ?? "",
            DatabaseConnectionString = config["DATABASE_CONNECTION_STRING"] ?? "",
            AdminApiKey = config["ADMIN_API_KEY"] ?? "",
        };

        var usesPostgresVectorStore = loreBotOptions.VectorStoreProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase);
        if (usesPostgresVectorStore)
        {
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(loreBotOptions.DatabaseConnectionString, n => n.UseVector()));
        }

        // Embeddings + vector store selected by the configured provider profile.
        services.AddRagProviders(loreBotOptions);

        services.AddScoped<TextChunker>(_ => new TextChunker(512, 64));
        services.AddScoped<IndexingPipeline>();
        services.AddHttpClient<WikiScraper>();

        if (usesPostgresVectorStore)
        {
            services.AddScoped<IRateLimitService>(sp =>
                new RateLimitService(sp.GetRequiredService<AppDbContext>(), 10, 50, 200));
            services.AddScoped<ICacheService, CacheService>();
        }
        else
        {
            // Cheap/serverless profile: soft per-instance guard, no database dependency.
            services.AddSingleton<IRateLimitService>(_ => new InMemoryRateLimitService(10, 50, 200));
            services.AddSingleton<ICacheService, NoOpCacheService>();
        }
        services.AddSingleton<InputGuardRails>();
        services.AddSingleton<OutputGuardRails>();

        // Chat client: DeepSeek (OpenAI-compatible endpoint) or OpenAI, selected by LLM_PROVIDER.
        OpenAIClient chatClient;
        string chatModelName;
        var deepSeekKey = string.IsNullOrWhiteSpace(loreBotOptions.DeepSeekApiKey) ? null : loreBotOptions.DeepSeekApiKey;
        if (loreBotOptions.LlmProvider.Equals("deepseek", StringComparison.OrdinalIgnoreCase) && deepSeekKey is not null)
        {
            chatClient = new OpenAIClient(
                new System.ClientModel.ApiKeyCredential(deepSeekKey),
                new OpenAIClientOptions { Endpoint = new Uri("https://api.deepseek.com/v1") });
            chatModelName = "deepseek-chat";
        }
        else
        {
            var openAiKey = string.IsNullOrWhiteSpace(loreBotOptions.OpenAiApiKey) ? "sk-placeholder" : loreBotOptions.OpenAiApiKey;
            chatClient = new OpenAIClient(openAiKey);
            chatModelName = loreBotOptions.ChatModel;
        }

        // OpenTelemetry → Azure Monitor
        var appInsightsConnStr = config["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("LoreBot"))
            .WithTracing(b =>
            {
                b.AddSource("LoreBot").AddHttpClientInstrumentation();
                if (!string.IsNullOrEmpty(appInsightsConnStr))
                    b.AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsightsConnStr);
            });

        // Rate limiting lives at the HTTP edge (per-IP in ChatFunction); the chat-client pipeline
        // deliberately omits a second instance-wide limiter to avoid double counting.
        services.AddScoped<IChatClient>(sp =>
        {
            var inner = chatClient.GetChatClient(chatModelName).AsIChatClient();
            return inner.AsBuilder()
                .UseFunctionInvocation()
                .Use(next => new ObservabilityChatClient(next, "lorebot"))
                .Use(next => new GuardRailsChatClient(
                    next,
                    sp.GetRequiredService<InputGuardRails>(),
                    sp.GetRequiredService<OutputGuardRails>(),
                    sp.GetRequiredService<ILogger<GuardRailsChatClient>>()))
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Build();
        });

        services.AddScoped<LoreSearchTools>(sp => new LoreSearchTools(
            sp.GetRequiredService<IEmbeddingService>(),
            sp.GetRequiredService<IVectorSearchService>()));

        services.AddScoped<IChatService>(sp => new ChatService(
            sp.GetRequiredService<IEmbeddingService>(),
            sp.GetRequiredService<IVectorSearchService>(),
            sp.GetRequiredService<IChatClient>(),
            loreBotOptions.DefaultUniverseName,
            sp.GetRequiredService<ICacheService>(),
            sp.GetRequiredService<LoreSearchTools>()));
    })
    .Build();

host.Run();
