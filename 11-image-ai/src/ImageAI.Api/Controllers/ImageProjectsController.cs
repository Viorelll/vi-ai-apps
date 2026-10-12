using ImageAI.Contracts.Dto;
using ImageAI.Services.Images;
using Microsoft.AspNetCore.Mvc;

namespace ImageAI.Api.Controllers;

[ApiController]
[Route("api/images/projects")]
public sealed class ImageProjectsController(ImageProjectService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var p = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }

    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> Generate(
        Guid id,
        GenerateRequest request,
        CancellationToken ct
    ) => Accepted($"/api/images/projects/{id}", await service.GenerateAsync(id, request, ct));

    [HttpPost("{id:guid}/edits")]
    public async Task<IActionResult> Edit(Guid id, EditRequest request, CancellationToken ct) =>
        Accepted($"/api/images/projects/{id}", await service.EditAsync(id, request, ct));

    [HttpPost("{id:guid}/variants")]
    public async Task<IActionResult> Variant(
        Guid id,
        VariantRequest request,
        CancellationToken ct
    ) => Accepted($"/api/images/projects/{id}", await service.VariantAsync(id, request, ct));
}
