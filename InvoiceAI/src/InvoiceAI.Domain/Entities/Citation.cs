namespace InvoiceAI.Domain.Entities;

public sealed class Citation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId
    {
        get; set;
    }
    public Guid? DocumentId
    {
        get; set;
    }
    public string SourceId { get; set; } = "";
    public int Generation
    {
        get; set;
    }
    public int? PageNumber
    {
        get; set;
    }
    public string Excerpt { get; set; } = "";
}
