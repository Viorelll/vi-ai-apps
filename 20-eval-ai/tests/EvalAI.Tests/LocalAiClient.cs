using EvalAI.Domain.DTOs;
using EvalAI.Domain.Models;
using EvalAI.Infrastructure.Abstractions;
using EvalAI.Infrastructure.Ai;
using EvalAI.Infrastructure.Extraction;
namespace EvalAI.Tests;

public sealed class LocalAiClient(FoundryClient foundry, DocumentExtractor extractor) : IAiClient
{
    public int CandidateCalls { get; private set; }
    public int JudgeCalls { get; private set; }
    public int FailJudgeCount { get; set; }
    public int JudgeStatus { get; set; } = 503;
    public async Task<AiStepResult> StepAsync(AiStepRequest r, CancellationToken ct) { if (r.Step == "candidate") CandidateCalls++; else { JudgeCalls++; if (FailJudgeCount-- > 0) throw new AppException(JudgeStatus, "test_judge_unavailable", "Test provider failure."); } return await foundry.ExecuteAsync(r, ct); }
    public Task<ExtractionResult> StartAsync(ExtractionStart r, CancellationToken ct) => extractor.StartAsync(r, ct);
    public Task<ExtractionResult> PollAsync(ExtractionPoll r, CancellationToken ct) => extractor.PollAsync(r, ct);
    public Task<AiStepResult> ChatAsync(ChatAiRequest r, CancellationToken ct) => foundry.ChatAsync(r, ct);
}
