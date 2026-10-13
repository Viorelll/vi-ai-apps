using System.Text.Json;
using AccessReadAI.Api.Models;
using AccessReadAI.Contracts;
using AccessReadAI.Database;
using AccessReadAI.Infrastructure.Storage;
using AccessReadAI.Services.Readings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Api.Controllers;

[ApiController, Route("api/readings")]
public sealed class ReadingsController(
    ReadingService readings,
    AppDbContext db,
    IObjectStore storage
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateReadingRequest request, CancellationToken ct)
    {
        var result = await readings.CreateAsync(request, ct);
        return Created($"/api/readings/{result.Id}", ReadingView.From(result));
    }

    [HttpGet]
    public async Task<object> List(CancellationToken ct) =>
        await db
            .Readings.OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.DocumentId,
                x.State,
                x.Revision,
            })
            .Take(100)
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<object> Get(Guid id, CancellationToken ct) =>
        ReadingView.From(await readings.LoadAsync(id, ct));

    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> Generate(
        Guid id,
        GenerateRequest request,
        CancellationToken ct
    )
    {
        await readings.GenerateAsync(id, request, ct);
        return Accepted();
    }

    [HttpPatch("{id:guid}/variants")]
    public async Task<IActionResult> Correct(
        Guid id,
        CorrectVariantRequest request,
        CancellationToken ct
    )
    {
        await readings.CorrectAsync(id, request, ct);
        return Accepted();
    }

    [HttpGet("{id:guid}/audio/{segmentId:guid}")]
    public async Task<IActionResult> Audio(Guid id, Guid segmentId, CancellationToken ct)
    {
        var reading = await readings.LoadAsync(id, ct);
        if (reading.State == "Stale")
            throw new ApiFault(410, "source_deleted");
        var variant =
            reading
                .Blocks.SelectMany(x => x.Variants)
                .SingleOrDefault(x => x.Narration.Any(n => n.Id == segmentId))
            ?? throw new ApiFault(404, "audio_not_found");
        var segment = variant.Narration.Single(x => x.Id == segmentId);
        if (segment.Stale || segment.TextHash != variant.TextHash)
            throw new ApiFault(410, "audio_stale");
        var obj = await storage.OpenAsync(segment.ObjectKey, ct);
        Response.OnCompleted(() =>
        {
            obj.Dispose();
            return Task.CompletedTask;
        });
        return File(obj.ResponseStream, segment.ContentType);
    }

    [HttpGet("{id:guid}/images/{blockId:guid}")]
    public async Task<IActionResult> Image(Guid id, Guid blockId, CancellationToken ct)
    {
        var reading = await readings.LoadAsync(id, ct);
        if (reading.State == "Stale")
            throw new ApiFault(410, "source_deleted");
        var block =
            reading.Blocks.SingleOrDefault(x => x.Id == blockId && x.ImageObjectKey != null)
            ?? throw new ApiFault(404, "image_not_found");
        var obj = await storage.OpenAsync(block.ImageObjectKey!, ct);
        Response.OnCompleted(() =>
        {
            obj.Dispose();
            return Task.CompletedTask;
        });
        return File(obj.ResponseStream, "image/png");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct) =>
        File(
            JsonSerializer.SerializeToUtf8Bytes(
                ReadingView.From(await readings.LoadAsync(id, ct)),
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }
            ),
            "application/json",
            $"reading-{id}.json"
        );
}
