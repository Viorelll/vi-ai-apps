using Microsoft.AspNetCore.Mvc;
using ShopVisionAI.Ai.Providers;
using ShopVisionAI.Contracts.Dto;

namespace ShopVisionAI.Ai.Controllers;

[ApiController]
[Route("internal/vision")]
public sealed class VisionController(FoundryVisionProvider provider) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<VisionResponse>> Inspect(
        VisionRequest input,
        CancellationToken ct
    ) => Ok(await provider.InspectAsync(input, ct));
}
