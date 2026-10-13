using System.Security.Cryptography;
using System.Text;

namespace AccessReadAI.Ai.Middleware;

public sealed class InternalKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }
        var expected =
            config["Internal:Key"]
            ?? throw new InvalidOperationException("Internal key is required");
        var supplied = context.Request.Headers["X-Internal-Key"].ToString();
        if (
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied))
            )
        )
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { code = "invalid_internal_key" });
            return;
        }
        await next(context);
    }
}
