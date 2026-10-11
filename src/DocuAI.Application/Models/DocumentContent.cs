namespace DocuAI.Application.Models;

public sealed record DocumentContent(Stream Stream, string ContentType, string FileName);
