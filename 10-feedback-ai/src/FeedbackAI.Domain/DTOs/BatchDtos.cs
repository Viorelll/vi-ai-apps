namespace FeedbackAI.Domain.DTOs;

public sealed record CreateBatchRequest(IReadOnlyList<string> Messages);
public sealed record LabelCorrectionRequest(long Revision, string Field, string Value);
public sealed record FeedbackItemDto(Guid Id, int Ordinal, string RedactedText, string Topic, string SuggestedTopic, string Sentiment, string Urgency, string Reason, string Provider);
public sealed record BatchDto(Guid Id, string State, long Revision, int Total, IReadOnlyDictionary<string, int> Topics, IReadOnlyList<FeedbackItemDto> Items);
