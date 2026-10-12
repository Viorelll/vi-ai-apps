using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using InvoiceAI.Ai.Configuration;
using InvoiceAI.Ai.Providers.Extraction;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Tests.Fixtures;
namespace InvoiceAI.Tests.Ai;

public sealed class DocumentIntelligenceTests
{
    [Theory]
    [InlineData("invoice", 119)]
    [InlineData("receipt", 120)]
    public void MapsTypedFieldsAndEvidence(string kind, decimal total)
    {
        var doc = Guid.NewGuid();
        var e = InvoiceFixture.Extraction(doc, kind);
        Assert.Equal(total, e.Fields.Single(x => x.Path == "total").Value.GetDecimal());
        Assert.Equal("MDL", e.Fields.Single(x => x.Path == "currency").Value.GetString());
        Assert.All(e.Fields, x => Assert.All(x.Evidence, s => { Assert.Equal(doc, s.DocumentId); Assert.Equal(1, s.PageNumber); Assert.Contains("boundingRegion", s.RegionJson!); }));
        Assert.Single(e.Lines);
        Assert.Equal(100, e.Lines[0].Amount);
    }
    [Fact]
    public void OcrAmbiguityRemainsNull()
    {
        using var json = JsonDocument.Parse("""{"analyzeResult":{"documents":[{"fields":{"InvoiceTotal":{"type":"currency","content":"1O0,00","confidence":0.4}}}]}}""");
        var e = DocumentIntelligenceParser.Parse(Guid.NewGuid(), "invoice", json.RootElement);
        var f = Assert.Single(e.Fields);
        Assert.Equal(JsonValueKind.Null, f.Value.ValueKind);
        Assert.Equal("1O0,00", f.RawText);
    }
    [Theory]
    [InlineData("http://example.cognitiveservices.azure.com/documentintelligence/documentModels/a/results/x")]
    [InlineData("https://evil.example/documentintelligence/documentModels/a/results/x")]
    [InlineData("https://example.cognitiveservices.azure.com:8443/documentintelligence/documentModels/a/results/x")]
    [InlineData("https://example.cognitiveservices.azure.com/other")]
    public void OperationUrlRejectsSsrf(string value)
    {
        var client = new DocumentIntelligenceClient(new HttpClient(), null!, Options.Create(new ProviderOptions { DocumentEndpoint = "https://example.cognitiveservices.azure.com/" }));
        Assert.Throws<GatewayException>(() => client.ValidateOperation(value));
    }
    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(400, false)]
    [InlineData(404, false)]
    public void ClassifiesRetry(int status, bool transient)
    {
        using var response = new HttpResponseMessage((HttpStatusCode)status);
        Assert.Equal(transient, Assert.Throws<GatewayException>(() => DocumentIntelligenceClient.Check(response)).Transient);
    }
    [Fact]
    public void RejectsMultipleInvoicesInOneDocument()
    {
        using var json = JsonDocument.Parse("""{"analyzeResult":{"documents":[{},{}]}}""");
        Assert.Throws<GatewayException>(() => DocumentIntelligenceParser.Parse(Guid.NewGuid(), "invoice", json.RootElement));
    }
}
