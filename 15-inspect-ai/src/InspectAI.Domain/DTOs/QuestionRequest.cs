namespace InspectAI.Domain.DTOs;

public sealed record QuestionRequest(string Question, Guid RequestId, int Revision);
