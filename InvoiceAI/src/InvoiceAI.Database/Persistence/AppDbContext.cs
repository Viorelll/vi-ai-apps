using Microsoft.EntityFrameworkCore;
using InvoiceAI.Domain.Entities;
namespace InvoiceAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<ExportSnapshot> ExportSnapshots => Set<ExportSnapshot>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("app");
        m.Entity<Document>().ToTable("Documents");
        m.Entity<Document>().HasIndex(x => x.Sha256).IsUnique().HasFilter("\"Status\" <> 'Deleted'");
        m.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        m.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        m.Entity<Invoice>().ToTable("Invoices");
        m.Entity<Invoice>().HasIndex(x => x.DocumentId).IsUnique();
        m.Entity<Invoice>().Property(x => x.Revision).IsConcurrencyToken();
        m.Entity<Invoice>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Invoice>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Invoice>().HasMany(x => x.Fields).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Invoice>().HasMany(x => x.Issues).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<InvoiceLine>().ToTable("InvoiceLines");
        m.Entity<InvoiceLine>().HasIndex(x => new { x.InvoiceId, x.Ordinal }).IsUnique();
        m.Entity<ExtractedField>().ToTable("ExtractedFields");
        m.Entity<ExtractedField>().HasIndex(x => new { x.InvoiceId, x.Path }).IsUnique();
        m.Entity<ExtractedField>().Property(x => x.ValueJson).HasColumnType("jsonb");
        m.Entity<ExtractedField>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        m.Entity<ExtractedField>().Property(x => x.CorrectedValueJson).HasColumnType("jsonb");
        m.Entity<ValidationIssue>().ToTable("ValidationIssues");
        m.Entity<ValidationIssue>().Property(x => x.DetailsJson).HasColumnType("jsonb");
        m.Entity<IngestionJob>().ToTable("IngestionJobs");
        m.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        m.Entity<IngestionJob>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<IngestionJob>().HasIndex(x => new { x.TargetId, x.PipelineVersion, x.Kind }).IsUnique();
        m.Entity<IngestionJob>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        m.Entity<ExportSnapshot>().ToTable("ExportSnapshots");
        m.Entity<ExportSnapshot>().HasIndex(x => new { x.InvoiceId, x.Revision, x.SchemaVersion }).IsUnique();
        m.Entity<ExportSnapshot>().Property(x => x.PayloadJson).HasColumnType("jsonb");
        m.Entity<ExportSnapshot>().HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId);
        m.Entity<DocumentChunk>().ToTable("DocumentChunks");
        m.Entity<DocumentChunk>().HasIndex(x => new { x.DocumentId, x.Generation, x.Ordinal }).IsUnique();
        m.Entity<DocumentChunk>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId);
        m.Entity<Conversation>().ToTable("Conversations");
        m.Entity<Conversation>().HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId);
        m.Entity<Message>().ToTable("Messages");
        m.Entity<Message>().HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId);
        m.Entity<Citation>().ToTable("Citations");
        m.Entity<Citation>().HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId);
        m.Entity<Citation>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        foreach (var entity in m.Model.GetEntityTypes())
            foreach (var p in entity.GetProperties())
            {
                if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
                {
                    p.SetPrecision(18);
                    p.SetScale(6);
                }
                if (p.ClrType == typeof(string) && p.GetColumnType() != "jsonb")
                    if (p.GetMaxLength() is null)
                        p.SetMaxLength(p.Name is "Text" or "Excerpt" or "Explanation" ? 16000 : 2000);
            }
    }
}
