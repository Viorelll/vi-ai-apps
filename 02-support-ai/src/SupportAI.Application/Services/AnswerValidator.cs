using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
namespace SupportAI.Application.Services;

public static class AnswerValidator
{
    public static void Validate(AiAnswer answer, DomainStepRequest request)
    {
        var labels = request.Sources.Select(s => s.Label).ToHashSet();
        if (string.IsNullOrWhiteSpace(answer.Answer) || answer.Answer.Length > 12000 ||
            answer.CitationLabels is null || answer.CitationLabels.Count > 8 ||
            !new[] { "answered", "waiting_for_evidence", "action_proposed" }.Contains(answer.Status) ||
            answer.CitationLabels.Any(l => !labels.Contains(l)))
            throw new SupportException("invalid_ai_answer", 502);
        if (answer.ProposedAction is { } action &&
           (action.Kind is not ("ticket" or "return") || string.IsNullOrWhiteSpace(action.Summary) || action.Summary.Length > 500 ||
            action.EvidenceLabels is null || action.EvidenceLabels.Any(l => !labels.Contains(l)) ||
            (action.Kind == "return" && request.Order?.ReturnEligible != true)))
            throw new SupportException("invalid_ai_proposal", 502);
        if ((answer.Status == "action_proposed") != (answer.ProposedAction is not null))
            throw new SupportException("invalid_ai_status", 502);
        if (request.Sources.Count > 0 && answer.CitationLabels.Count == 0)
            throw new SupportException("missing_citations", 502);
    }
}
