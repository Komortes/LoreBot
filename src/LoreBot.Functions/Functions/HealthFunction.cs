using System.Net;
using LoreBot.Infrastructure.Database;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBot.Functions.Functions;

public class HealthFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    public HealthFunction(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    [Function("Health")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        FunctionContext ctx)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db is null)
        {
            var noDatabase = req.CreateResponse(HttpStatusCode.OK);
            await noDatabase.WriteAsJsonAsync(new { status = "healthy", database = (bool?)null });
            return noDatabase;
        }

        bool dbOk;
        try { dbOk = await db.Database.CanConnectAsync(ctx.CancellationToken); }
        catch { dbOk = false; }
        var resp = req.CreateResponse(dbOk ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        await resp.WriteAsJsonAsync(new { status = dbOk ? "healthy" : "degraded", database = dbOk });
        return resp;
    }
}
