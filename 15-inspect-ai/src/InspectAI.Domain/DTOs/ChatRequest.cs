namespace InspectAI.Domain.DTOs;

public sealed record ChatRequest(Guid InspectionId, string Question, Guid RequestId, int Revision);
