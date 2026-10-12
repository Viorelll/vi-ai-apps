using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Api.Results;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Database;
using VideoSearchAI.Domain.Models;
using VideoSearchAI.Services.Services;

namespace VideoSearchAI.Api.Controllers;

[ApiController, Route("api/video/library")]
public sealed class LibraryController(
    LibraryService library,
    SearchService search,
    IDbContextFactory<AppDbContext> factory
) : ControllerBase
{
    [HttpGet]
    public Task<VideoDto[]> List(CancellationToken ct) => library.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<VideoDto> Get(Guid id, CancellationToken ct) =>
        (await library.ListAsync(ct)).SingleOrDefault(x => x.Id == id)
        ?? throw new DomainException("video_not_found", 404);

    [HttpPost]
    public async Task<IActionResult> Create(CreateVideoRequest r, CancellationToken ct)
    {
        var v = await library.CreateAsync(r, ct);
        return Created($"/api/video/library/{v.Id}", v);
    }

    [HttpPost("{id:guid}/index")]
    public async Task<IActionResult> Index(Guid id, IndexVideoRequest r, CancellationToken ct)
    {
        var jobId = await library.IndexAsync(id, r.Revision, ct);
        return Accepted($"/api/jobs/{jobId}", new { jobId });
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search(SearchRequest r, CancellationToken ct)
    {
        var searchRunId = await search.CreateAsync(r, ct);
        return Accepted($"/api/video/library/search/{searchRunId}", new { searchRunId });
    }

    [HttpGet("search/{id:guid}")]
    public Task<SearchRunDto> Result(Guid id, CancellationToken ct) => search.GetAsync(id, ct);

    [HttpGet("{id:guid}/content"), HttpHead("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d =
            await (
                from v in db.Videos
                join doc in db.Documents on v.DocumentId equals doc.Id
                where v.Id == id && !doc.Deleted
                select doc
            ).SingleOrDefaultAsync(ct) ?? throw new DomainException("video_not_found", 404);
        return new VideoContentResult(d);
    }

    [HttpGet("{id:guid}/segments")]
    public async Task<SegmentDto[]> Segments(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (
            from v in db.Videos
            join d in db.Documents on v.DocumentId equals d.Id
            join s in db.Segments on v.Id equals s.VideoId
            where v.Id == id && !d.Deleted && s.Generation == d.ActiveGeneration
            orderby s.Ordinal
            select new SegmentDto(
                s.Id,
                v.Id,
                d.Id,
                s.Generation,
                s.Ordinal,
                s.StartMs,
                s.EndMs,
                s.Transcript,
                s.VisualText,
                s.SourceId
            )
        ).ToArrayAsync(ct);
    }
}
