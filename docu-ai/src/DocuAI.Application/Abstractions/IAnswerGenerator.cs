namespace DocuAI.Application.Abstractions;

public interface IAnswerGenerator
{
    Task<string> GenerateAsync(string question, IReadOnlyList<(string Label, string FileName, string Text)> sources, CancellationToken cancellationToken);
}

