using System.Text.Json;
using Amazon.S3;
using InspectAI.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InspectAI.Infrastructure.Http;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, code, detail) = ex switch
            {
                AppException app => (app.Status, app.Code, app.Message),
                JsonException => (
                    502,
                    "invalid_ai_json",
                    "Provider returned an invalid JSON schema."
                ),
                BadHttpRequestException bad => (
                    bad.StatusCode,
                    "invalid_request",
                    "Invalid request or upload exceeds server limits."
                ),
                OperationCanceledException => (
                    503,
                    "operation_timeout",
                    "Operation canceled or timed out."
                ),
                _ => (
                    500,
                    "internal_error",
                    "Operation failed. Consult server logs using the trace ID."
                ),
            };
            logger.LogWarning(
                "Operation failed: {Code}, type {Type}, trace {Trace}",
                code,
                ex.GetType().Name,
                context.TraceIdentifier
            );
            if (ex is AmazonS3Exception storageError)
                logger.LogWarning(
                    "Storage request failed: {StorageCode}, HTTP {Status}",
                    storageError.ErrorCode,
                    (int)storageError.StatusCode
                );
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = status,
                    Title = code,
                    Detail = detail,
                    Extensions =
                    {
                        ["code"] = code,
                        ["traceId"] = context.TraceIdentifier,
                        ["retryAfterSeconds"] = (ex as AppException)?.RetryAfterSeconds,
                    },
                }
            );
        }
    }
}
