using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using InvoiceAI.Infrastructure.Ai;
namespace InvoiceAI.Ai.Middleware;

public sealed class InternalKeyMiddleware(RequestDelegate next, IOptions<InternalOptions> options)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path == "/health/live")
        {
            await next(ctx);
            return;
        }
        var supplied = ctx.Request.Headers["X-Internal-Key"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Key))))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "INTERNAL_AUTH_REQUIRED"
            });
            return;
        }
        await next(ctx);
    }
}
