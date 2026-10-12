using System.Text.Json;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Ai.Providers.Extraction;
namespace InvoiceAI.Tests.Fixtures;

public static class InvoiceFixture
{
    public static Invoice Valid()
    {
        var id = Guid.NewGuid();
        var doc = Guid.NewGuid();
        var i = new Invoice { Id = id, DocumentId = doc, Document = new() { Id = doc, Status = "Ready" }, VendorName = "Seed", InvoiceNumber = "SEED", Currency = "MDL", Date = new(2026, 10, 11), Subtotal = 100, Tax = 20, Total = 120, State = "Validated", Lines = [new() { InvoiceId = id, Ordinal = 0, Quantity = 2, UnitPrice = 50, Amount = 100 }] };
        i.Fields = new[] { "vendorName", "date", "currency", "subtotal", "tax", "total", "lines.0.amount" }.Select(p => new ExtractedField { InvoiceId = id, Path = p, Confidence = 0.97m, EvidenceJson = JsonSerializer.Serialize(new[] { new InvoiceAI.Contracts.Dto.EvidenceDto(doc, $"{p}:0", 1, "seed", null) }) }).ToList();
        return i;
    }
    public static InvoiceAI.Contracts.Dto.ExtractionDto Extraction(Guid doc, string kind = "invoice")
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seeds", $"{kind}.json")));
        return DocumentIntelligenceParser.Parse(doc, kind, json.RootElement);
    }
}
