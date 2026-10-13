using System.Text;
using System.Text.Json;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Models;
using EvalAI.Services.Evaluations;
using Microsoft.AspNetCore.Mvc;
namespace EvalAI.Api.Controllers;

[ApiController, Route("api/evaluations")]
public sealed class EvaluationsController(DatasetService datasets, EvaluationService evaluations, VariantCatalog variants) : ControllerBase
{
    [HttpGet("datasets")] public async Task<IActionResult> Datasets(CancellationToken ct) => Ok(await datasets.ListAsync(ct));
    [HttpPost("datasets")] public async Task<IActionResult> CreateDataset(CreateDatasetRequest r, CancellationToken ct) { var d = await datasets.CreateAsync(r, ct); return Created($"/api/evaluations/datasets/{d.Id}", new { d.Id, d.Name, d.Version, d.Revision }); }
    [HttpGet("variants")] public IActionResult Variants() => Ok(new { mode = variants.Mode, variants = variants.All });
    [HttpGet("runs")] public async Task<IActionResult> Runs(CancellationToken ct) => Ok(await evaluations.ListAsync(ct));
    [HttpPost("runs")] public async Task<IActionResult> Run(CreateRunRequest r, CancellationToken ct) { var run = await evaluations.CreateAsync(r, ct); return Accepted($"/api/evaluations/runs/{run.Id}", new { run.Id, run.State, run.Revision }); }
    [HttpGet("runs/{id:guid}")] public async Task<IActionResult> Report(Guid id, CancellationToken ct) => Ok(await evaluations.ReportAsync(id, ct));
    [HttpPost("runs/{id:guid}/retry")] public async Task<IActionResult> Retry(Guid id, RevisionRequest r, CancellationToken ct) { await evaluations.RetryAsync(id, r.Revision, ct); return Accepted(); }
    [HttpPost("runs/{id:guid}/cancel")] public async Task<IActionResult> Cancel(Guid id, RevisionRequest r, CancellationToken ct) { await evaluations.CancelAsync(id, r.Revision, ct); return Accepted(); }
    [HttpGet("runs/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "json", CancellationToken ct = default)
    {
        var report = await evaluations.ReportAsync(id, ct);
        if (format == "json") return File(JsonSerializer.SerializeToUtf8Bytes(report, JsonDefaults.Options), "application/json", $"evalai-{id}.json");
        if (format == "jsonl") { var root = JsonSerializer.SerializeToElement(report, JsonDefaults.Options); var rows = root.GetProperty("results").EnumerateArray().Select(r => JsonDefaults.Serialize(new { scope = root.GetProperty("scope"), caseResult = r })); return File(Encoding.UTF8.GetBytes(string.Join("\n", rows) + "\n"), "application/x-ndjson", $"evalai-{id}.jsonl"); }
        if (format != "csv") throw new AppException(400, "invalid_export_format", "Use json, csv or jsonl.");
        var json = JsonSerializer.SerializeToElement(report, JsonDefaults.Options);
        string Safe(string? value) { value ??= ""; if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value; return "\"" + value.Replace("\"", "\"\"") + "\""; }
        var lines = new List<string> { "variant,case,state,checksPassed,judgeScore,candidateLatencyMs,candidateCost,judgeCost" };
        foreach (var r in json.GetProperty("results").EnumerateArray()) lines.Add(string.Join(",", new[] { Safe(r.GetProperty("variant").GetString()), Safe(r.GetProperty("caseName").GetString()), Safe(r.GetProperty("state").GetString()), r.GetProperty("checks").ValueKind == JsonValueKind.Null ? "" : r.GetProperty("checks").GetProperty("passed").ToString(), r.GetProperty("judge").ValueKind == JsonValueKind.Null ? "" : r.GetProperty("judge").GetProperty("score").ToString(), r.GetProperty("candidateLatencyMs").ToString(), r.GetProperty("candidateCost").GetProperty("amount").GetString() ?? "", r.GetProperty("judgeCost").GetProperty("amount").GetString() ?? "" }));
        return File(Encoding.UTF8.GetBytes(string.Join("\n", lines)), "text/csv", $"evalai-{id}.csv");
    }
    [HttpPost("rates")] public async Task<IActionResult> Rate(RateInput r, CancellationToken ct) => Created("/api/evaluations/rates", await evaluations.AddRateAsync(r, ct));
}
