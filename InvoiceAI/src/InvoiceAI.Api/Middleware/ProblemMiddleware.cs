using System.Diagnostics;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;
namespace InvoiceAI.Api.Middleware;

public sealed class ProblemMiddleware(RequestDelegate next, ILogger<ProblemMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception e) when (!context.Response.HasStarted)
        {
            var (status, code) = e switch
            {
                AppException a => (a.Status, a.Code),
                GatewayException g => (503, g.Code),
                BadHttpRequestException b => (b.StatusCode, "REQUEST_INVALID"),
                _ => (500, "INTERNAL_ERROR")
            };
            logger.LogWarning("Request failed with {Code}; trace {TraceId}", code, context.TraceIdentifier);
            context.Response.StatusCode = status;
            var problem = new ProblemDetails { Status = status, Title = code };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
    }
}
