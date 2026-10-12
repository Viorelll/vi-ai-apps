using System.Globalization;
using System.Text.Json;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
using InvoiceAI.Services.Mapping;
using InvoiceAI.Services.Validation;
namespace InvoiceAI.Services.Services;

public sealed class InvoiceService(IInvoiceRepository repository)
{
    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct) => (await repository.ListAsync(ct)).Select(InvoiceMapper.ToDto).ToArray();
    public async Task<InvoiceDto> GetAsync(Guid id, CancellationToken ct) => InvoiceMapper.ToDto(await repository.GetAsync(id, ct));
    public async Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("invoice" or "receipt"))
            throw new AppException("KIND_INVALID");
        return InvoiceMapper.ToDto(await repository.CreateAsync(request.DocumentId, request.Kind, ct));
    }
    public Task<Guid> AnalyzeAsync(Guid id, CancellationToken ct) => repository.AnalyzeAsync(id, ct);
    public async Task<InvoiceDto> CorrectAsync(Guid id, CorrectionRequest request, CancellationToken ct)
    {
        var i = await repository.GetAsync(id, ct);
        RequireEditable(i);
        if (request.Corrections.Count is 0 or > 200)
            throw new AppException("CORRECTIONS_INVALID");
        foreach (var (path, value) in request.Corrections)
        {
            try
            {
                SetField(i, path, value);
            }
            catch (Exception e) when (e is FormatException or InvalidOperationException or OverflowException) { throw new AppException("FIELD_VALUE_INVALID"); }
            var f = i.Fields.FirstOrDefault(x => x.Path == path);
            if (f is null)
            {
                f = new()
                {
                    InvoiceId = i.Id,
                    Path = path
                };
                i.Fields.Add(f);
            }
            f.CorrectedValueJson = value.GetRawText();
            f.CorrectedAtUtc = DateTimeOffset.UtcNow;
        }
        await ValidateCoreAsync(i, ct);
        i.Explanation = null;
        i.SuggestedCategory = null;
        await repository.SaveAsync(i, request.Revision, ct);
        return InvoiceMapper.ToDto(i);
    }
    public async Task<InvoiceDto> ValidateAsync(Guid id, CancellationToken ct)
    {
        var i = await repository.GetAsync(id, ct);
        RequireEditable(i);
        var revision = i.Revision;
        await ValidateCoreAsync(i, ct);
        await repository.SaveAsync(i, revision, ct);
        return InvoiceMapper.ToDto(i);
    }
    public async Task<ExportSnapshot> ExportAsync(Guid id, CancellationToken ct)
    {
        var i = await repository.GetAsync(id, ct);
        RequireEditable(i);
        if (i.State is not ("Validated" or "Exported") || i.Issues.Any(x => x.Severity == "error"))
            throw new AppException("EXPORT_BLOCKED", 409);
        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = "1",
            i.Id,
            i.Revision,
            vendor = i.VendorName,
            i.InvoiceNumber,
            i.Date,
            i.Currency,
            amounts = new
            {
                i.Subtotal,
                i.Tax,
                i.Total
            },
            lines = i.Lines.Select(InvoiceMapper.Line),
            evidence = i.Fields.Select(InvoiceMapper.Field),
            corrections = i.Fields.Where(x => x.CorrectedValueJson is not null).ToDictionary(x => x.Path, x => JsonSerializer.Deserialize<JsonElement>(x.CorrectedValueJson!))
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return await repository.ExportAsync(i, payload, ct);
    }
    public async Task ValidateCoreAsync(Invoice i, CancellationToken ct)
    {
        i.Issues = InvoiceRules.Validate(i).ToList();
        if (await repository.HasDuplicateAsync(i, ct))
            i.Issues.Add(new()
            {
                InvoiceId = i.Id,
                Code = "POSSIBLE_DUPLICATE",
                Path = "invoiceNumber",
                Severity = "warning"
            });
        i.State = i.Issues.Any(x => x.Severity == "error") ? "NeedsReview" : "Validated";
    }
    private static void RequireEditable(Invoice i)
    {
        if (i.State is "Uploaded" or "Extracting" or "Failed" or "Stale" || i.Document.Status != "Ready")
            throw new AppException("STATE_CONFLICT", 409);
    }
    private static void SetField(Invoice i, string path, JsonElement v)
    {
        string? Text() => v.ValueKind == JsonValueKind.Null ? null : v.GetString();
        decimal? Number() => v.ValueKind == JsonValueKind.Null ? null : v.GetDecimal();
        switch (path)
        {
            case "vendorName":
                i.VendorName = Text();
                break;
            case "invoiceNumber":
                i.InvoiceNumber = Text();
                break;
            case "date":
                i.Date = v.ValueKind == JsonValueKind.Null ? null : DateOnly.ParseExact(v.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                break;
            case "currency":
                i.Currency = Text()?.ToUpperInvariant();
                break;
            case "subtotal":
                i.Subtotal = Number();
                break;
            case "tax":
                i.Tax = Number();
                break;
            case "total":
                i.Total = Number();
                break;
            default:
                var parts = path.Split('.');
                if (parts.Length != 3 || parts[0] != "lines" || !int.TryParse(parts[1], out var ordinal))
                    throw new AppException("FIELD_PATH_INVALID");
                var l = i.Lines.SingleOrDefault(x => x.Ordinal == ordinal) ?? throw new AppException("FIELD_PATH_INVALID");
                switch (parts[2])
                {
                    case "description":
                        l.Description = Text();
                        break;
                    case "quantity":
                        l.Quantity = Number();
                        break;
                    case "unitPrice":
                        l.UnitPrice = Number();
                        break;
                    case "amount":
                        l.Amount = Number();
                        break;
                    case "currency":
                        l.Currency = Text()?.ToUpperInvariant();
                        break;
                    default:
                        throw new AppException("FIELD_PATH_INVALID");
                }
                break;
        }
        if (v.ValueKind == JsonValueKind.String && v.GetString()!.Length > 1000)
            throw new AppException("FIELD_VALUE_INVALID");
        if (v.ValueKind == JsonValueKind.Number && Math.Abs(v.GetDecimal()) > 999999999999m)
            throw new AppException("FIELD_VALUE_INVALID");
    }
}
