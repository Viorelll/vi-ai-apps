using Microsoft.AspNetCore.Mvc;
using ShopVisionAI.Ai.Providers;
using ShopVisionAI.Contracts.Dto;

namespace ShopVisionAI.Ai.Controllers;

[ApiController]
[Route("internal/grounded")]
public sealed class GroundedController(FoundryGroundedProvider provider) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Generate(GroundedRequest input, CancellationToken ct) =>
        Ok(await provider.GenerateAsync(input, ct));
}
