using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Api.Middleware;

public sealed class ProblemMiddleware(RequestDelegate next, ILogger<ProblemMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
                throw;
            var (status, code, message) = ex switch
            {
                AppException app => (app.Status, app.Code, app.Message),
                DbUpdateConcurrencyException => (
                    409,
                    "revision_conflict",
                    "The resource changed. Reload and try again."
                ),
                DbUpdateException => (
                    409,
                    "persistence_conflict",
                    "The operation conflicted with existing data."
                ),
                BadHttpRequestException b => (
                    b.StatusCode,
                    "invalid_request",
                    "Invalid request body."
                ),
                _ => (
                    500,
                    "internal_error",
                    "The operation failed. Use the trace ID for diagnosis."
                ),
            };
            logger.LogWarning(ex, "Request failed {Code} {TraceId}", code, context.TraceIdentifier);
            await Results
                .Problem(
                    statusCode: status,
                    title: message,
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", code },
                        { "traceId", context.TraceIdentifier },
                    }
                )
                .ExecuteAsync(context);
        }
    }
}
