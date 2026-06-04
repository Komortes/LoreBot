using System.Net;
using LoreBot.Infrastructure.Database;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace LoreBot.Functions.Functions;

public class HealthFunction
{
    private readonly AppDbContext _db;
    public HealthFunction(AppDbContext db) => _db = db;

    [Function("Health")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        FunctionContext ctx)
    {
        bool dbOk;
        try { dbOk = await _db.Database.CanConnectAsync(ctx.CancellationToken); }
        catch { dbOk = false; }
        var resp = req.CreateResponse(dbOk ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        await resp.WriteAsJsonAsync(new { status = dbOk ? "healthy" : "degraded", database = dbOk });
        return resp;
    }
}
