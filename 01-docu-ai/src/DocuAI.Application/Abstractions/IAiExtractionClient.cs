namespace DocuAI.Application.Abstractions;

public interface IAiExtractionClient
{
    Task<string> ExtractAsync(string fileName, Stream content, CancellationToken cancellationToken);
}

