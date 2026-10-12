using System.Security.Cryptography;
using System.Text.Json;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Services.Abstractions;
namespace InvoiceAI.Ai.Providers.Extraction;

public sealed class DemoExtractionProvider(IObjectStore storage)
{
    private static string Seeds => Path.Combine(AppContext.BaseDirectory, "Seeds");
    public async Task<StartExtractionResponse> StartAsync(StartExtractionRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("invoice" or "receipt") || !request.ObjectKey.StartsWith($"originals/{request.DocumentId:N}/", StringComparison.Ordinal))
            throw new GatewayException("EXTRACTION_INPUT_INVALID", false);
        await using var input = await storage.OpenAsync(request.ObjectKey, ct);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct)).ToLowerInvariant();
        var expected = (await File.ReadAllTextAsync(Path.Combine(Seeds, $"{request.Kind}.sha256"), ct)).Trim();
        if (hash != expected)
            throw new GatewayException("DEMO_SEED_ONLY", false);
        return new($"demo://extraction/{request.DocumentId:N}/{request.Kind}/{hash}");
    }
    public async Task<PollExtractionResponse> PollAsync(PollExtractionRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("invoice" or "receipt"))
            throw new GatewayException("EXTRACTION_INPUT_INVALID", false);
        var hash = (await File.ReadAllTextAsync(Path.Combine(Seeds, $"{request.Kind}.sha256"), ct)).Trim();
        if (request.OperationUrl != $"demo://extraction/{request.DocumentId:N}/{request.Kind}/{hash}")
            throw new GatewayException("OPERATION_URL_INVALID", false);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Seeds, $"{request.Kind}.json"), ct));
        return new("succeeded", DocumentIntelligenceParser.Parse(request.DocumentId, request.Kind, json.RootElement) with
        {
            Provider = "DemoFixture"
        });
    }
}
