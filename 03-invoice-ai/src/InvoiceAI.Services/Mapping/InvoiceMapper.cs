using System.Text.Json;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Domain.Entities;
namespace InvoiceAI.Services.Mapping;

public static class InvoiceMapper
{
    public static FieldDto Field(ExtractedField f) => new(f.Path, f.RawText, JsonSerializer.Deserialize<JsonElement>(f.ValueJson), f.Confidence, JsonSerializer.Deserialize<List<EvidenceDto>>(f.EvidenceJson)!, f.CorrectedValueJson is null ? null : JsonSerializer.Deserialize<JsonElement>(f.CorrectedValueJson));
    public static LineDto Line(InvoiceLine l) => new(l.Ordinal, l.Description, l.Quantity, l.UnitPrice, l.Amount, l.Currency, l.PageNumber);
    public static IssueDto Issue(ValidationIssue i) => new(i.Code, i.Severity, i.Path, i.DetailsJson);
    public static InvoiceDto ToDto(Invoice i) => new(i.Id, i.DocumentId, i.Kind, i.State, i.Revision, i.VendorName, i.InvoiceNumber, i.Date, i.Currency, i.Subtotal, i.Tax, i.Total, i.Explanation, i.SuggestedCategory, i.ErrorCode, i.InputTokens, i.OutputTokens, i.Lines.OrderBy(x => x.Ordinal).Select(Line).ToArray(), i.Fields.OrderBy(x => x.Path).Select(Field).ToArray(), i.Fields.Where(x => x.CorrectedValueJson is not null).ToDictionary(x => x.Path, x => JsonSerializer.Deserialize<JsonElement>(x.CorrectedValueJson!)), i.Issues.Select(Issue).ToArray());
}
