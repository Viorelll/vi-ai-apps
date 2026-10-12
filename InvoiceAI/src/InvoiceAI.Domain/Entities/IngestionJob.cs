namespace InvoiceAI.Domain.Entities;

public sealed class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId
    {
        get; set;
    }
    public Guid? TargetId
    {
        get; set;
    }
    public string Kind { get; set; } = "Analyze";
    public string PipelineVersion { get; set; } = "invoice-v1";
    public string Status { get; set; } = "Queued";
    public string Phase { get; set; } = "Start";
    public string? ProviderOperation
    {
        get; set;
    }
    public string? RawObjectKey
    {
        get; set;
    }
    public Guid? LeaseToken
    {
        get; set;
    }
    public DateTimeOffset? LeaseUntilUtc
    {
        get; set;
    }
    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Attempt
    {
        get; set;
    }
    public int PollCount
    {
        get; set;
    }
    public string? ErrorCode
    {
        get; set;
    }
}
