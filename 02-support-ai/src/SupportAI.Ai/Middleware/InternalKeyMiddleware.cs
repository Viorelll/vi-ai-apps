using System.Security.Cryptography;
using System.Text;
namespace SupportAI.Ai.Middleware;

public sealed class InternalKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/health/live")
        {
            await next(context);
            return;
        }
        var key = config["Internal:Key"];
        var supplied = context.Request.Headers["X-Internal-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
        {
            context.Response.StatusCode = 401;
            return;
        }
        await next(context);
    }
}
