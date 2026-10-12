namespace ProcureAI.Application.Models;

public sealed record RunSummaryDto(Guid Id, string Brief, string State, long Revision, bool Stale);
