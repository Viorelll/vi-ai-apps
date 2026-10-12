namespace FeedbackAI.Application.Abstractions;

public sealed record ClassificationInput(Guid ItemId, string RedactedText);
public sealed record ClassificationOutput(string Topic, string Sentiment, string Urgency, string Reason, string Provider, string ModelVersion);
public interface IClassifier { Task<ClassificationOutput> ClassifyAsync(ClassificationInput input, CancellationToken cancellationToken); }
