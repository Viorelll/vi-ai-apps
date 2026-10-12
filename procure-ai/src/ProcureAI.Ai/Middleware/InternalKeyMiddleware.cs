using System.Security.Cryptography;
using System.Text;

namespace ProcureAI.Ai.Middleware;

public sealed class InternalKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var expected = configuration["Internal:Key"] ?? "";
            var supplied = context.Request.Headers["X-Internal-Key"].ToString();
            if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied)))
            { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        }
        await next(context);
    }
}
