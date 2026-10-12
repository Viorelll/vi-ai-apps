using System.Text.Json;
using System.Globalization;
using InvoiceAI.Domain.Entities;
namespace InvoiceAI.Services.Validation;

public static class InvoiceRules
{
    public static IReadOnlyList<ValidationIssue> Validate(Invoice invoice, decimal confidenceThreshold = 0.8m)
    {
        var issues = new List<ValidationIssue>();
        void Add(string code, string path, object? details = null, string severity = "error") => issues.Add(new()
        {
            InvoiceId = invoice.Id,
            Code = code,
            Path = path,
            Severity = severity,
            DetailsJson = JsonSerializer.Serialize(details ?? new
            {
            })
        });
        if (string.IsNullOrWhiteSpace(invoice.Currency))
            Add("CURRENCY_REQUIRED", "currency");
        else if (!CurrencyCodes.Contains(invoice.Currency))
            Add("CURRENCY_INVALID", "currency");
        if (string.IsNullOrWhiteSpace(invoice.VendorName))
            Add("VENDOR_REQUIRED", "vendorName");
        if (invoice.Date is null)
            Add("DATE_REQUIRED", "date");
        var tolerance = invoice.Currency is "JPY" or "KRW" ? 1m : 0.01m;
        if (invoice.Subtotal is null || invoice.Tax is null || invoice.Total is null)
            Add("AMOUNT_REQUIRED", "amounts");
        else if (Math.Abs(invoice.Subtotal.Value + invoice.Tax.Value - invoice.Total.Value) > tolerance)
            Add("TOTAL_MISMATCH", "total", new
            {
                difference = invoice.Total - invoice.Subtotal - invoice.Tax,
                tolerance
            });
        if (invoice.Lines.Count == 0 || invoice.Lines.Any(x => x.Amount is null))
            Add("LINE_AMOUNT_REQUIRED", "lines");
        else if (invoice.Subtotal is decimal subtotal && Math.Abs(invoice.Lines.Sum(x => x.Amount!.Value) - subtotal) > tolerance)
            Add("LINE_SUM_MISMATCH", "subtotal");
        foreach (var line in invoice.Lines)
        {
            if (line.Currency is not null && line.Currency != invoice.Currency)
                Add("MIXED_CURRENCY", $"lines.{line.Ordinal}.currency");
            if (line.Quantity is decimal q && line.UnitPrice is decimal p && line.Amount is decimal a && Math.Abs(q * p - a) > tolerance)
                Add("LINE_TOTAL_MISMATCH", $"lines.{line.Ordinal}.amount");
        }
        foreach (var path in new[] { "vendorName", "date", "currency", "subtotal", "tax", "total" }.Concat(invoice.Lines.Select(x => $"lines.{x.Ordinal}.amount")))
        {
            var f = invoice.Fields.FirstOrDefault(x => x.Path == path);
            if (f?.CorrectedValueJson is not null)
                continue;
            if (f is null || f.Confidence is null || f.Confidence < confidenceThreshold)
                Add("CONFIDENCE_REVIEW", path);
            if (f is null || f.EvidenceJson == "[]")
                Add("EVIDENCE_MISSING", path);
        }
        return issues;
    }
    private static readonly HashSet<string> CurrencyCodes = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(c => new RegionInfo(c.Name).ISOCurrencySymbol).Append("MDL").ToHashSet(StringComparer.Ordinal);
}
