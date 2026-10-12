using System.Diagnostics;
using ImageAI.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace ImageAI.Api.Middleware;

public sealed class ProblemMiddleware(RequestDelegate next, ILogger<ProblemMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e) when (!context.Response.HasStarted)
        {
            var status =
                e is DomainException d ? d.Status
                : e is BadHttpRequestException b ? b.StatusCode
                : 500;
            var code =
                e is DomainException error ? error.Code
                : status == 413 ? "file_size_limit"
                : "request_failed";
            if (status >= 500)
                logger.LogError(
                    "Request failed: {Type}; trace {TraceId}",
                    e.GetType().Name,
                    context.TraceIdentifier
                );
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = status,
                    Title = code,
                    Extensions =
                    {
                        ["code"] = code,
                        ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
                    },
                }
            );
        }
    }
}
