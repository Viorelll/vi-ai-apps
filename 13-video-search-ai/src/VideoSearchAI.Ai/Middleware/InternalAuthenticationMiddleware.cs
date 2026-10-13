using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;

namespace VideoSearchAI.Ai.Middleware;

public sealed class InternalAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<AiOptions> options)
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var actual = Encoding.UTF8.GetBytes(
                context.Request.Headers["X-Internal-Key"].ToString()
            );
            var expected = Encoding.UTF8.GetBytes(options.Value.InternalKey);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                context.Response.StatusCode = 401;
                return;
            }
        }
        await next(context);
    }
}
