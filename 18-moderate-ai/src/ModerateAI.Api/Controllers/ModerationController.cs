using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModerateAI.Database;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Domain.Models;
using ModerateAI.Services;

namespace ModerateAI.Api.Controllers;

[ApiController]
[Route("api/moderation")]
public sealed class ModerationController(
    ModerationService service,
    IDbContextFactory<AppDbContext> factory,
    IObjectStore store
) : ControllerBase
{
    [HttpPost("items")]
    public async Task<IActionResult> Create(CreateItemRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct);
        return Created(
            $"/api/moderation/items/{item.Id}",
            new
            {
                item.Id,
                item.Revision,
                item.State,
            }
        );
    }

    [HttpPost("items/{id:guid}/analyze")]
    public async Task<IActionResult> Analyze(Guid id, CancellationToken ct)
    {
        Guid jobId = await service.AnalyzeAsync(id, ct);
        return Accepted($"/api/moderation/items/{id}", new { jobId });
    }

    [HttpGet("items/{id:guid}")]
    public Task<ItemResult> Get(Guid id, [FromQuery] bool includeInput, CancellationToken ct) =>
        service.GetAsync(id, includeInput, ct);

    [HttpGet("items")]
    public async Task<object> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        return new
        {
            Items = await db
                .ModerationItems.AsNoTracking()
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.State,
                    x.Revision,
                    x.Stale,
                    x.CreatedAtUtc,
                })
                .ToArrayAsync(ct),
        };
    }

    [HttpGet("review")]
    public Task<object> Queue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) => service.QueueAsync(page, pageSize, ct);

    [HttpPost("items/{id:guid}/review")]
    public async Task<ItemResult> Review(Guid id, ReviewRequest request, CancellationToken ct)
    {
        await service.ReviewAsync(id, request, ct);
        return await service.GetAsync(id, false, ct);
    }

    [HttpGet("items/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct) =>
        File(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                await service.GetAsync(id, false, ct),
                new System.Text.Json.JsonSerializerOptions(
                    System.Text.Json.JsonSerializerDefaults.Web
                )
                {
                    WriteIndented = true,
                }
            ),
            "application/json",
            $"moderation-{id}.json"
        );

    [HttpGet("items/{id:guid}/raw")]
    public async Task<IActionResult> Raw(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var analysis =
            await db.SafetyAnalyses.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == id, ct)
            ?? throw new DomainException(404, "analysis_not_found");
        return File(
            await store.ReadAsync(analysis.RawObjectKey, ct),
            "application/json",
            $"raw-{id}.json"
        );
    }
}
