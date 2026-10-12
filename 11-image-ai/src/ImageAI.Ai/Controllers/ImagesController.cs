using System.Security.Cryptography;
using System.Text;
using ImageAI.Ai.Options;
using ImageAI.Ai.Providers;
using ImageAI.Contracts.Dto;
using ImageAI.Domain.Models;
using ImageAI.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ImageAI.Ai.Controllers;

[ApiController]
[Route("internal/images")]
public sealed class ImagesController(IImageProvider provider, IOptions<ImagesOptions> options)
    : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(GatewayRequest input, CancellationToken ct)
    {
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.InternalApiKey));
        var supplied = SHA256.HashData(
            Encoding.UTF8.GetBytes(Request.Headers["X-Internal-Key"].ToString())
        );
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
            return Unauthorized(new { code = "internal_auth_failed" });
        try
        {
            if (
                input.OperationId == Guid.Empty
                || input.Prompt.Length is < 5 or > 4000
                || input.Size is not ("1024x1024" or "1536x1024" or "1024x1536")
                || input.Quality is not ("low" or "medium" or "high")
            )
                throw new DomainException("invalid_gateway_request");
            if (input.MaskBase64 is not null && input.ImageBase64 is null)
                throw new DomainException("mask_input_required");
            if (input.ImageBase64 is { } image)
            {
                if (image.Length > 70_000_000)
                    throw new DomainException("image_size_limit", 413);
                var info = ImageValidator.Inspect(Convert.FromBase64String(image));
                if (input.MaskBase64 is { } mask)
                {
                    if (mask.Length > 70_000_000)
                        throw new DomainException("mask_size_limit", 413);
                    ImageValidator.ValidateMask(
                        info.Width,
                        info.Height,
                        Convert.FromBase64String(mask)
                    );
                }
            }
            return Ok(await provider.GenerateAsync(input, ct));
        }
        catch (ProviderException e)
        {
            if (e.RetryAfter > 0)
                Response.Headers.RetryAfter = e.RetryAfter.ToString();
            return StatusCode(
                e.Status,
                new { code = e.Message, traceId = HttpContext.TraceIdentifier }
            );
        }
        catch (DomainException e)
        {
            return StatusCode(
                e.Status,
                new { code = e.Code, traceId = HttpContext.TraceIdentifier }
            );
        }
        catch (FormatException)
        {
            return BadRequest(
                new { code = "invalid_base64", traceId = HttpContext.TraceIdentifier }
            );
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            return StatusCode(
                502,
                new { code = "provider_operation_uncertain", traceId = HttpContext.TraceIdentifier }
            );
        }
    }
}
