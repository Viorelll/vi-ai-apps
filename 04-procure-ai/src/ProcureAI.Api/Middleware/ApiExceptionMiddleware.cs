using System.Text.Json;
using ProcureAI.Application.Exceptions;

namespace ProcureAI.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            var known = exception as AppException;
            var code = known?.Code ?? (exception is JsonException ? "invalid_json" : "service_unavailable");
            var status = known?.Status ?? (exception is JsonException ? 400 : 503);
            if (known is null) logger.LogWarning("Request {TraceId} failed: {ErrorType}", context.TraceIdentifier, exception.GetType().Name);
            await Results.Problem(statusCode: status, title: "Cererea nu a putut fi finalizată", extensions: new Dictionary<string, object?>
            { ["code"] = code, ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }
    }
}
