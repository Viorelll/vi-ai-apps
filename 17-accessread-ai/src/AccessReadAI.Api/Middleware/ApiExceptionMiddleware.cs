using AccessReadAI.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Api.Middleware;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e) when (!context.Response.HasStarted)
        {
            var status = e switch
            {
                ApiFault f => f.Status,
                DbUpdateConcurrencyException => 409,
                ProviderFault => 502,
                _ => 500,
            };
            var code = e switch
            {
                ApiFault f => f.Code,
                DbUpdateConcurrencyException => "revision_conflict",
                ProviderFault => e.Message,
                _ => "internal_error",
            };
            logger.LogWarning(
                "Request failed with {Code}; trace {TraceId}",
                code,
                context.TraceIdentifier
            );
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "about:blank",
                    title = code,
                    status,
                    code,
                    traceId = context.TraceIdentifier,
                }
            );
        }
    }
}
