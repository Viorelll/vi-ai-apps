using DocuAI.Domain.DTOs;

namespace DocuAI.Application.Models;

public sealed record DocumentUploadResult(DocumentDto Document, bool Deduplicated);
