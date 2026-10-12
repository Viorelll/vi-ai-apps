using FeedbackAI.Api.Services;
using FeedbackAI.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace FeedbackAI.Api.Controllers;

[ApiController]
[Route("api/feedback")]
public sealed class FeedbackController(FeedbackService service) : ControllerBase
{
    [HttpPost("batches")]
    public async Task<IActionResult> Create(CreateBatchRequest request, CancellationToken ct) => StatusCode(201, await service.CreateAsync(request.Messages, ct));
    [HttpPost("batches/import")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file.Length is 0 or > 50 * 1024 * 1024) return BadRequest(new { code = "invalid_file_size" });
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".json" and not ".txt") return BadRequest(new { code = "unsupported_file_type" });
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(ct);
        IReadOnlyList<string> messages;
        try
        {
            messages = extension == ".json"
                ? System.Text.Json.JsonSerializer.Deserialize<string[]>(content) ?? []
                : content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.TrimEnd('\r')).ToArray();
        }
        catch (System.Text.Json.JsonException) { return BadRequest(new { code = "invalid_json" }); }
        return StatusCode(201, await service.CreateAsync(messages, ct));
    }
    [HttpPost("batches/{id:guid}/analyze")]
    public async Task<IActionResult> Analyze(Guid id, CancellationToken ct) => Accepted(value: await service.AnalyzeAsync(id, ct));
    [HttpGet("batches/{id:guid}")]
    public Task<BatchDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpGet("batches/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var batch = await service.GetAsync(id, ct);
        var csv = "id,topic,sentiment,urgency,redactedText\n" + string.Join("\n", batch.Items.Select(x => string.Join(',', Csv(x.Id.ToString()), Csv(x.Topic), Csv(x.Sentiment), Csv(x.Urgency), Csv(x.RedactedText))));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"feedback-{id}.csv");
    }
    [HttpPatch("items/{id:guid}/labels")]
    public async Task<IActionResult> Correct(Guid id, LabelCorrectionRequest request, CancellationToken ct) { await service.CorrectAsync(id, request, ct); return NoContent(); }
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
