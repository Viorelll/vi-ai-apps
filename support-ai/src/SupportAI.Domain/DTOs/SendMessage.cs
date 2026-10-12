namespace SupportAI.Domain.DTOs;

public sealed record SendMessage(string Text, Guid RequestId);
