using ImageAI.Infrastructure.Storage;
using ImageAI.Services.Images;
using Microsoft.AspNetCore.Mvc;

namespace ImageAI.Api.Controllers;

[ApiController]
[Route("api/images/assets")]
public sealed class ImageAssetsController(ImageProjectService service, ObjectStore store)
    : ControllerBase
{
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var asset = await service.AssetAsync(id, ct);
        Response.Headers.ETag = $"\"{asset.Sha256}\"";
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return File(
            await store.ReadAsync(asset.ObjectKey, ct),
            "image/png",
            enableRangeProcessing: true
        );
    }
}
