using InspectAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InspectAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionSource> InspectionSources => Set<InspectionSource>();
    public DbSet<VisualFinding> VisualFindings => Set<VisualFinding>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<InspectionReport> InspectionReports => Set<InspectionReport>();
    public DbSet<QuestionMessage> QuestionMessages => Set<QuestionMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        foreach (
            var type in new[]
            {
                typeof(Document),
                typeof(DocumentChunk),
                typeof(IngestionJob),
                typeof(Inspection),
                typeof(InspectionSource),
                typeof(VisualFinding),
                typeof(ChecklistItem),
                typeof(InspectionReport),
                typeof(QuestionMessage),
            }
        )
        {
            var e = model.Entity(type);
            e.ToTable(type.Name + "s");
            e.HasKey("Id");
            foreach (var p in e.Metadata.GetProperties().Where(p => p.ClrType == typeof(string)))
                p.SetMaxLength(100000);
        }
        model.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        model.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        model
            .Entity<Document>()
            .HasIndex(x => x.Sha256)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Deleting'");
        model
            .Entity<DocumentChunk>()
            .HasIndex(x => new
            {
                x.DocumentId,
                x.Generation,
                x.Ordinal,
            })
            .IsUnique();
        model
            .Entity<DocumentChunk>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        model.Entity<DocumentChunk>().Property(x => x.RegionJson).HasColumnType("jsonb");
        model
            .Entity<IngestionJob>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
        model.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        model.Entity<IngestionJob>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        model.Entity<Inspection>().Property(x => x.Revision).IsConcurrencyToken();
        model
            .Entity<InspectionSource>()
            .HasIndex(x => new { x.InspectionId, x.DocumentId })
            .IsUnique();
        model
            .Entity<InspectionSource>()
            .HasOne<Inspection>()
            .WithMany()
            .HasForeignKey(x => x.InspectionId);
        model
            .Entity<InspectionSource>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        model
            .Entity<VisualFinding>()
            .HasOne<Inspection>()
            .WithMany()
            .HasForeignKey(x => x.InspectionId);
        model
            .Entity<VisualFinding>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<VisualFinding>().Property(x => x.RegionJson).HasColumnType("jsonb");
        model
            .Entity<ChecklistItem>()
            .HasOne<Inspection>()
            .WithMany()
            .HasForeignKey(x => x.InspectionId);
        model
            .Entity<ChecklistItem>()
            .HasOne<VisualFinding>()
            .WithMany()
            .HasForeignKey(x => x.FindingId)
            .OnDelete(DeleteBehavior.Cascade);
        model.Entity<ChecklistItem>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        model
            .Entity<InspectionReport>()
            .HasOne<Inspection>()
            .WithMany()
            .HasForeignKey(x => x.InspectionId);
        model
            .Entity<InspectionReport>()
            .HasIndex(x => new
            {
                x.InspectionId,
                x.InputHash,
                x.PromptVersion,
            })
            .IsUnique();
        model.Entity<InspectionReport>().Property(x => x.ResultJson).HasColumnType("jsonb");
        model.Entity<InspectionReport>().Property(x => x.SourcesJson).HasColumnType("jsonb");
        model
            .Entity<QuestionMessage>()
            .HasOne<Inspection>()
            .WithMany()
            .HasForeignKey(x => x.InspectionId);
        model
            .Entity<QuestionMessage>()
            .HasIndex(x => new { x.InspectionId, x.RequestId })
            .IsUnique();
        model.Entity<QuestionMessage>().Property(x => x.AnswerJson).HasColumnType("jsonb");
    }
}
