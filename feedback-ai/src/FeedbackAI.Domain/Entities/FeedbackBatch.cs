namespace FeedbackAI.Domain.Entities;

public sealed class FeedbackBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TaxonomyVersion { get; set; } = "taxonomy-1";
    public string State { get; set; } = "Imported";
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<FeedbackItem> Items { get; set; } = [];
}
