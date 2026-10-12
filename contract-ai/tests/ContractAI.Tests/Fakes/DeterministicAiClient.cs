using System.Text.Json;
using ContractAI.Domain.DTOs;
using ContractAI.Infrastructure.Abstractions;
using ContractAI.Infrastructure.Extraction;
using ContractAI.Services.Contracts;

namespace ContractAI.Tests.Fakes;

// Test-only adapter. Never registered in production; storage and extraction remain real.
public sealed class DeterministicAiClient(DocumentExtractor extractor) : IAiClient
{
    public int Calls { get; private set; }

    public Task<ExtractionResult> StartAsync(ExtractionStart input, CancellationToken ct) =>
        extractor.StartAsync(input, ct);

    public Task<ExtractionResult> PollAsync(ExtractionPoll input, CancellationToken ct) =>
        extractor.PollAsync(input, ct);

    public Task<AiResult> StepAsync(AiRequest input, CancellationToken ct)
    {
        Calls++;
        object output = input.Step switch
        {
            "analyze" => new AnalysisResult(
                "Contract demonstrativ.",
                ["Data recepției"],
                input
                    .Sources.Where(s => s.Text.Contains("Beneficiarul plătește"))
                    .Select(s => new ObligationCandidate(
                        s.Id,
                        "Beneficiarul",
                        "Plată 1000 EUR",
                        null,
                        s.Text.Contains("30 zile")
                            ? "30 zile de la recepție"
                            : "15 zile de la recepție",
                        [new(s.Id, s.Text)]
                    ))
                    .ToArray(),
                []
            ),
            "compare" => new ComparisonResult(
                "Termenul se schimbă de la 30 la 15 zile.",
                input
                    .Pairs!.Where(p => p.Kind == "modified")
                    .Select(p => new FindingCandidate(
                        "PAYMENT_TERM_CHANGED",
                        "needs_review",
                        "Termen mai scurt; verificați evenimentul de recepție.",
                        [
                            new(p.LeftClauseId!.Value, p.BeforeText!),
                            new(p.RightClauseId!.Value, p.AfterText!),
                        ]
                    ))
                    .ToArray()
            ),
            _ => input.Question!.Contains("jurisdic", StringComparison.OrdinalIgnoreCase)
                ? new QuestionResult("Informația nu este disponibilă în versiunea selectată.", [])
                : new QuestionResult(
                    "Termenul este 15 zile de la recepție.",
                    input
                        .Sources.Where(s => s.Text.Contains("Beneficiarul plătește"))
                        .Select(s => new Evidence(s.Id, s.Text))
                        .ToArray()
                ),
        };
        return Task.FromResult(
            new AiResult(
                JsonSerializer.Serialize(output, ContractService.Json),
                "test-only",
                10,
                5,
                1
            )
        );
    }
}
