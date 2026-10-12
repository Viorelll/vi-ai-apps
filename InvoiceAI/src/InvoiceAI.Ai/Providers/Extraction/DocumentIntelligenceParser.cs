using System.Text.Json;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Infrastructure.Ai;
namespace InvoiceAI.Ai.Providers.Extraction;

public static class DocumentIntelligenceParser
{
    public static ExtractionDto Parse(Guid documentId, string kind, JsonElement root)
    {
        if (!root.TryGetProperty("analyzeResult", out var result) || !result.TryGetProperty("documents", out var docs) || docs.GetArrayLength() != 1)
            throw new GatewayException("EXTRACTION_CONTRACT_INVALID", false);
        var fields = docs[0].GetProperty("fields");
        var output = new List<FieldDto>();
        var map = kind == "receipt" ? new Dictionary<string, string> { { "MerchantName", "vendorName" }, { "TransactionDate", "date" }, { "Subtotal", "subtotal" }, { "TotalTax", "tax" }, { "Total", "total" }, { "Currency", "currency" } } : new Dictionary<string, string> { { "VendorName", "vendorName" }, { "InvoiceId", "invoiceNumber" }, { "InvoiceDate", "date" }, { "SubTotal", "subtotal" }, { "TotalTax", "tax" }, { "InvoiceTotal", "total" }, { "CurrencyCode", "currency" } };
        foreach (var (provider, path) in map)
            if (fields.TryGetProperty(provider, out var f))
                output.Add(Field(documentId, path, f));
        if (!output.Any(x => x.Path == "currency"))
        {
            var key = kind == "receipt" ? "Total" : "InvoiceTotal";
            if (fields.TryGetProperty(key, out var money) && money.TryGetProperty("valueCurrency", out var vc) && vc.TryGetProperty("currencyCode", out var code))
                output.Add(Field(documentId, "currency", money) with
                {
                    Value = code.Clone()
                });
        }
        var lines = new List<LineDto>();
        if (fields.TryGetProperty("Items", out var items) && items.TryGetProperty("valueArray", out var array))
        {
            foreach (var item in array.EnumerateArray())
            {
                if (lines.Count >= 200)
                    throw new GatewayException("EXTRACTION_LIMIT", false);
                var obj = item.GetProperty("valueObject");
                var ordinal = lines.Count;
                JsonElement? F(string name) => obj.TryGetProperty(name, out var f) ? f : null;
                decimal? Num(string name)
                {
                    var f = F(name);
                    return f is JsonElement v && Value(v).TryGetDecimalSafe(out var n) ? n : null;
                }
                string? Text(string name) => F(name) is JsonElement f && Value(f).ValueKind == JsonValueKind.String ? Value(f).GetString() : null;
                var amountKey = kind == "receipt" ? "TotalPrice" : "Amount";
                var amount = F(amountKey);
                var page = amount is JsonElement a ? Evidence(documentId, $"lines.{ordinal}.amount", a).FirstOrDefault()?.PageNumber : null;
                string? currency = null;
                if (amount is JsonElement aa && aa.TryGetProperty("valueCurrency", out var av) && av.TryGetProperty("currencyCode", out var ac))
                    currency = ac.GetString();
                lines.Add(new(ordinal, Text("Description"), Num("Quantity"), Num(kind == "receipt" ? "Price" : "UnitPrice"), Num(amountKey), currency, page));
                foreach (var (name, path) in new[] { ("Description", "description"), ("Quantity", "quantity"), (kind == "receipt" ? "Price" : "UnitPrice", "unitPrice"), (amountKey, "amount") })
                    if (F(name) is JsonElement f)
                        output.Add(Field(documentId, $"lines.{ordinal}.{path}", f));
            }
        }
        return new(documentId, "DocumentIntelligence", result.TryGetProperty("modelId", out var model) ? model.GetString()! : kind, root.GetRawText(), output, lines);
    }
    private static bool TryGetDecimalSafe(this JsonElement v, out decimal d)
    {
        d = 0;
        return v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out d);
    }
    private static JsonElement Value(JsonElement f)
    {
        foreach (var key in new[] { "valueString", "valueDate", "valueNumber", "valueInteger" })
            if (f.TryGetProperty(key, out var v))
                return v.Clone();
        if (f.TryGetProperty("valueCurrency", out var currency) && currency.TryGetProperty("amount", out var amount))
            return amount.Clone();
        return JsonSerializer.SerializeToElement<object?>(null);
    }
    private static FieldDto Field(Guid doc, string path, JsonElement f) => new(path, f.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "", Value(f), f.TryGetProperty("confidence", out var score) && score.TryGetDecimal(out var confidence) ? confidence : null, Evidence(doc, path, f));
    private static IReadOnlyList<EvidenceDto> Evidence(Guid doc, string path, JsonElement f)
    {
        if (!f.TryGetProperty("boundingRegions", out var regions))
            return [];
        return regions.EnumerateArray().Select((region, index) => new EvidenceDto(doc, $"{doc:N}:{path}:{index}", region.GetProperty("pageNumber").GetInt32(), f.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "", JsonSerializer.Serialize(new { boundingRegion = region, spans = f.TryGetProperty("spans", out var spans) ? spans : JsonSerializer.SerializeToElement(Array.Empty<object>()) }))).ToArray();
    }
}
