using ContractAI.Database.Persistence;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Models;
using ContractAI.Services.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ContractAI.Api.Controllers;

[ApiController, Route("api/contracts")]
public sealed class ContractsController(
    ContractService contracts,
    IDbContextFactory<AppDbContext> factory
) : ControllerBase
{
    [HttpGet("versions")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return Ok(
            await db
                .ContractVersions.AsNoTracking()
                .OrderBy(x => x.ContractKey)
                .ThenBy(x => x.VersionLabel)
                .ToArrayAsync(ct)
        );
    }

    [HttpPost("versions")]
    public async Task<IActionResult> Create(CreateVersionRequest input, CancellationToken ct)
    {
        var version = await contracts.CreateAsync(input, ct);
        return Created($"/api/contracts/versions/{version.Id}", version);
    }

    [HttpGet("versions/{id:guid}")]
    public Task<object> Get(Guid id, CancellationToken ct) => contracts.GetAsync(id, ct);

    [HttpPost("versions/{id:guid}/analyze")]
    public async Task<IActionResult> Analyze(Guid id, AnalyzeRequest input, CancellationToken ct) =>
        Accepted(new { jobId = await contracts.AnalyzeAsync(id, input.Revision, ct) });

    [HttpPost("compare")]
    public async Task<IActionResult> Compare(CompareRequest input, CancellationToken ct)
    {
        var comparison = await contracts.CompareAsync(input, ct);
        return Accepted(
            $"/api/contracts/comparisons/{comparison.Id}",
            new { comparisonId = comparison.Id, comparison.State }
        );
    }

    [HttpGet("comparisons/{id:guid}")]
    public async Task<IActionResult> Comparison(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item =
            await db.Comparisons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "comparison_not_found", "Comparison not found.");
        return Ok(
            new
            {
                item.Id,
                item.State,
                result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                    item.ResultJson
                ),
                item.Deployment,
                item.InputTokens,
                item.OutputTokens,
            }
        );
    }

    [HttpPost("versions/{id:guid}/questions"), EnableRateLimiting("ai")]
    public Task<AnswerDto> Question(Guid id, QuestionRequest input, CancellationToken ct) =>
        contracts.QuestionAsync(id, input, ct);
}
