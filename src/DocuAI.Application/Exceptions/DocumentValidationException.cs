namespace DocuAI.Application.Exceptions;

public sealed class DocumentValidationException(string message) : Exception(message) { }
