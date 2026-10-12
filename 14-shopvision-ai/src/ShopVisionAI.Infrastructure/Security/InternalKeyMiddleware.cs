using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ShopVisionAI.Infrastructure.Security;

public sealed class InternalKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var expected = config["Internal:Key"] ?? "";
            var provided = context.Request.Headers["X-Internal-Key"].ToString();
            if (
                expected.Length < 16
                || !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                    SHA256.HashData(Encoding.UTF8.GetBytes(provided))
                )
            )
            {
                context.Response.StatusCode = 401;
                return;
            }
        }
        await next(context);
    }
}
