namespace TutorAI.Domain.DTOs;

public sealed record ChatRequest(Guid SessionId, string Question, long Revision);
