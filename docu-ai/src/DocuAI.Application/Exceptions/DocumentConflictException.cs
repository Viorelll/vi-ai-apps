namespace DocuAI.Application.Exceptions;

public sealed class DocumentConflictException(string message) : Exception(message) { }
