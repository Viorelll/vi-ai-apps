using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShopVisionAI.Infrastructure.Http;

namespace ShopVisionAI.Ai.Middleware;

public sealed class AiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct
    )
    {
        var status = exception switch
        {
            HttpRequestException h when h.StatusCode != null => (int)h.StatusCode,
            ArgumentException => 422,
            TaskCanceledException => 504,
            _ => 502,
        };
        if (exception is ProviderCallException p && p.RetryAfter is { } delay)
            context.Response.Headers.RetryAfter = Math.Clamp(
                    (int)Math.Ceiling(delay.TotalSeconds),
                    1,
                    120
                )
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = "ai_provider_failed",
                Extensions =
                {
                    { "code", "ai_provider_failed" },
                    { "traceId", context.TraceIdentifier },
                },
            },
            ct
        );
        return true;
    }
}
