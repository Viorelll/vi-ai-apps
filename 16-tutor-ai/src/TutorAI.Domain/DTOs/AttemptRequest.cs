namespace TutorAI.Domain.DTOs;

public sealed record AttemptRequest(string Answer, Guid RequestId, long Revision);
