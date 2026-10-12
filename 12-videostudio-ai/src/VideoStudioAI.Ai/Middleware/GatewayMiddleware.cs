using System.Security.Cryptography;
using System.Text;
using VideoStudioAI.Ai.Models;

namespace VideoStudioAI.Ai.Middleware;

public sealed class GatewayMiddleware(
    RequestDelegate next,
    IConfiguration config,
    ILogger<GatewayMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var expected = SHA256.HashData(Encoding.UTF8.GetBytes(config["InternalKey"] ?? ""));
            var actual = SHA256.HashData(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-Internal-Key"].ToString())
            );
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                context.Response.StatusCode = 401;
                return;
            }
        }
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gateway request failed {TraceId}", context.TraceIdentifier);
            var e = ex as GatewayException;
            await Results
                .Problem(
                    statusCode: e?.Status ?? 502,
                    title: e?.Message ?? "AI operation failed.",
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", e?.Code ?? "provider_response_invalid" },
                        { "traceId", context.TraceIdentifier },
                    }
                )
                .ExecuteAsync(context);
        }
    }
}
