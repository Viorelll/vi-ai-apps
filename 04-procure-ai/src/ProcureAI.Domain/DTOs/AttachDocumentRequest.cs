namespace ProcureAI.Domain.DTOs;

public sealed record AttachDocumentRequest(Guid DocumentId, string Role, long Revision);
