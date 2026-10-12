using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Domain.Models;

namespace VideoSearchAI.Api.Middleware;

public sealed class ProblemMiddleware(RequestDelegate next, ILogger<ProblemMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            if (context.Response.HasStarted)
                throw;
            var code = e switch
            {
                DomainException d => d.Code,
                DbUpdateConcurrencyException => "revision_conflict",
                BadHttpRequestException => "invalid_request",
                _ => "internal_error",
            };
            var status = e switch
            {
                DomainException d => d.Status,
                DbUpdateConcurrencyException => 409,
                BadHttpRequestException b => b.StatusCode,
                _ => 500,
            };
            logger.LogWarning(
                "Request {TraceId}: {Code}, {ErrorType}",
                context.TraceIdentifier,
                code,
                e.GetType().Name
            );
            await Microsoft
                .AspNetCore.Http.Results.Problem(
                    statusCode: status,
                    title: code,
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
