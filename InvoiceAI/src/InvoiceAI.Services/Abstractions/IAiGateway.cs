using InvoiceAI.Contracts.Dto;
namespace InvoiceAI.Services.Abstractions;

public interface IAiGateway
{
    Task<StartExtractionResponse> StartAsync(StartExtractionRequest request, CancellationToken ct);
    Task<PollExtractionResponse> PollAsync(PollExtractionRequest request, CancellationToken ct);
    Task<ExplanationDto> ExplainAsync(ExplainRequest request, CancellationToken ct);
    Task<bool> ReadyAsync(CancellationToken ct);
}
