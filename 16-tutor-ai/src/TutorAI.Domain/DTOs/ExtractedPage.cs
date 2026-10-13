namespace TutorAI.Domain.DTOs;

public sealed record ExtractedPage(int? PageNumber, string Text, string? RegionJson = null);
