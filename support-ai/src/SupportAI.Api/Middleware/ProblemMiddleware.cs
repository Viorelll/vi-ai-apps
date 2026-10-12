using Microsoft.EntityFrameworkCore;
using SupportAI.Domain.Models;
namespace SupportAI.Api.Middleware;

public sealed class ProblemMiddleware(RequestDelegate next, ILogger<ProblemMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            if (context.Response.HasStarted)
                throw;
            var (code, status) = e switch
            {
                SupportException s => (s.Code, s.Status),
                DbUpdateConcurrencyException => ("revision_conflict", 409),
                _ => ("internal_error", 500)
            };
            if (status == 500)
                logger.LogError("Request failed: {Type}; trace {TraceId}", e.GetType().Name, context.TraceIdentifier);
            await Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { { "code", code }, { "traceId", context.TraceIdentifier } }).ExecuteAsync(context);
        }
    }
}
