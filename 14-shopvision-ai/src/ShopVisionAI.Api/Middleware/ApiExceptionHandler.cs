using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopVisionAI.Services.Errors;

namespace ShopVisionAI.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct
    )
    {
        var status = exception switch
        {
            DomainException d => d.Status,
            ArgumentException => 400,
            DbUpdateConcurrencyException => 409,
            DbUpdateException => 409,
            _ => 500,
        };
        var code = exception switch
        {
            DomainException d => d.Message,
            ArgumentException => "invalid_request",
            DbUpdateException => "database_conflict",
            _ => "internal_error",
        };
        logger.LogWarning(
            "Request failed with {Code}; trace {TraceId}",
            code,
            context.TraceIdentifier
        );
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = code,
                Extensions = { { "code", code }, { "traceId", context.TraceIdentifier } },
            },
            ct
        );
        return true;
    }
}
