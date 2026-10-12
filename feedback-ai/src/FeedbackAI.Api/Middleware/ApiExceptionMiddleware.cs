using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAI.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            var (status, code) = ex switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, "invalid_request"),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "not_found"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "revision_conflict"),
                InvalidOperationException => (StatusCodes.Status409Conflict, "conflict"),
                HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "ai_provider_unavailable"),
                _ => (StatusCodes.Status500InternalServerError, "internal_error")
            };
            if (status >= 500) logger.LogError(ex, "Request failed with {Code}", code);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new { status, code, traceId = context.TraceIdentifier }, cancellationToken: context.RequestAborted);
        }
    }
}
