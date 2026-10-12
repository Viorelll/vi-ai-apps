using Microsoft.EntityFrameworkCore;
using ProcureAI.Domain.Entities;

namespace ProcureAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();
    public DbSet<DurableJob> Jobs => Set<DurableJob>();
    public DbSet<ProcurementRun> Runs => Set<ProcurementRun>();
    public DbSet<RunDocument> RunDocuments => Set<RunDocument>();
    public DbSet<AgentStep> Steps => Set<AgentStep>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        model.Entity<Document>(e =>
        {
            e.ToTable("Documents"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.Sha256).IsUnique().HasFilter("\"State\" <> 'Deleting'");
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.FileName).HasMaxLength(240);
            e.Property(x => x.State).HasMaxLength(24);
        });
        model.Entity<DocumentChunk>(e =>
        {
            e.ToTable("DocumentChunks"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DocumentId, x.Generation, x.Ordinal }).IsUnique();
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<ProcurementRun>(e =>
        {
            e.ToTable("ProcurementRuns"); e.HasKey(x => x.Id);
            e.Property(x => x.CriteriaJson).HasColumnType("jsonb");
            e.Property(x => x.Revision).IsConcurrencyToken();
        });
        model.Entity<RunDocument>(e =>
        {
            e.ToTable("RunDocuments"); e.HasKey(x => new { x.RunId, x.DocumentId });
            e.HasOne<ProcurementRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DurableJob>(e =>
        {
            e.ToTable("IngestionJobs"); e.HasKey(x => x.Id);
            e.Property(x => x.LeaseToken).IsConcurrencyToken();
            e.HasIndex(x => new { x.State, x.NextAttemptAtUtc });
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ProcurementRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<AgentStep>(e =>
        {
            e.ToTable("AgentSteps"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RunId, x.Role, x.InputHash, x.PromptVersion }).IsUnique();
            e.Property(x => x.OutputJson).HasColumnType("jsonb");
            e.HasOne<ProcurementRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Offer>(e =>
        {
            e.ToTable("Offers"); e.HasKey(x => x.Id);
            e.Property(x => x.Net).HasPrecision(18, 4); e.Property(x => x.Shipping).HasPrecision(18, 4);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.EvidenceJson).HasColumnType("jsonb");
            e.HasOne<ProcurementRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Recommendation>(e =>
        {
            e.ToTable("Recommendations"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RunId, x.SnapshotHash }).IsUnique();
            e.Property(x => x.ScoresJson).HasColumnType("jsonb"); e.Property(x => x.ReportJson).HasColumnType("jsonb");
            e.HasOne<ProcurementRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Conversation>(e =>
        {
            e.ToTable("Conversations"); e.HasKey(x => x.Id);
            e.HasOne<ProcurementRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Message>(e =>
        {
            e.ToTable("Messages"); e.HasKey(x => x.Id);
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Citation>(e =>
        {
            e.ToTable("Citations"); e.HasKey(x => x.Id);
            e.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<DocumentChunk>().WithMany().HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
