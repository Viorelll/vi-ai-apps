namespace TutorAI.Domain.DTOs;

public sealed record CreateSessionRequest(string Topic, int Difficulty = 1);
