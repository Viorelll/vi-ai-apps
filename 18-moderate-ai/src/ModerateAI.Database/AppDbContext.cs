using Microsoft.EntityFrameworkCore;
using ModerateAI.Domain.Entities;

namespace ModerateAI.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ModerationItem> ModerationItems => Set<ModerationItem>();
    public DbSet<SafetyAnalysis> SafetyAnalyses => Set<SafetyAnalysis>();
    public DbSet<CategoryFinding> CategoryFindings => Set<CategoryFinding>();
    public DbSet<PolicyDecision> PolicyDecisions => Set<PolicyDecision>();
    public DbSet<ReviewDecision> ReviewDecisions => Set<ReviewDecision>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        model.Entity<Document>().ToTable("Documents").HasKey(x => x.Id);
        model
            .Entity<Document>()
            .HasIndex(x => x.Sha256)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Deleted'");
        model.Entity<Document>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        model.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        model.Entity<ModerationItem>().ToTable("ModerationItems").HasKey(x => x.Id);
        model.Entity<ModerationItem>().HasIndex(x => x.RequestId).IsUnique();
        model.Entity<ModerationItem>().Property(x => x.Revision).IsConcurrencyToken();
        model
            .Entity<ModerationItem>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.ImageDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<SafetyAnalysis>().ToTable("SafetyAnalyses").HasKey(x => x.Id);
        model.Entity<SafetyAnalysis>().HasIndex(x => x.ItemId).IsUnique();
        model
            .Entity<SafetyAnalysis>()
            .HasOne<ModerationItem>()
            .WithMany()
            .HasForeignKey(x => x.ItemId);
        model.Entity<CategoryFinding>().ToTable("CategoryFindings").HasKey(x => x.Id);
        model
            .Entity<CategoryFinding>()
            .HasOne<SafetyAnalysis>()
            .WithMany()
            .HasForeignKey(x => x.AnalysisId);
        model.Entity<CategoryFinding>().HasIndex(x => new { x.AnalysisId, x.Category }).IsUnique();
        model.Entity<PolicyDecision>().ToTable("PolicyDecisions").HasKey(x => x.Id);
        model.Entity<PolicyDecision>().HasIndex(x => x.ItemId).IsUnique();
        model
            .Entity<PolicyDecision>()
            .HasOne<ModerationItem>()
            .WithMany()
            .HasForeignKey(x => x.ItemId);
        model.Entity<PolicyDecision>().Property(x => x.ReasonsJson).HasColumnType("jsonb");
        model.Entity<ReviewDecision>().ToTable("ReviewDecisions").HasKey(x => x.Id);
        model
            .Entity<ReviewDecision>()
            .HasOne<ModerationItem>()
            .WithMany()
            .HasForeignKey(x => x.ItemId);
        model
            .Entity<ReviewDecision>()
            .HasIndex(x => new { x.ItemId, x.ExpectedRevision })
            .IsUnique();
        model.Entity<IngestionJob>().ToTable("IngestionJobs").HasKey(x => x.Id);
        model.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        model.Entity<IngestionJob>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId);
        model
            .Entity<IngestionJob>()
            .HasOne<ModerationItem>()
            .WithMany()
            .HasForeignKey(x => x.TargetId);
        model.Entity<IngestionJob>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
    }
}
