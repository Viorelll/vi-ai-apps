using Microsoft.EntityFrameworkCore;
using SupportAI.Domain.Entities;
namespace SupportAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<SupportCase> SupportCases => Set<SupportCase>();
    public DbSet<CaseAttachment> CaseAttachments => Set<CaseAttachment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();
    public DbSet<ActionProposal> ActionProposals => Set<ActionProposal>();
    public DbSet<ActionExecution> ActionExecutions => Set<ActionExecution>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("app");
        b.Entity<Document>().ToTable("Documents");
        b.Entity<Document>().HasIndex(x => x.Sha256).IsUnique();
        b.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        b.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        b.Entity<DocumentChunk>().ToTable("DocumentChunks");
        b.Entity<DocumentChunk>().HasIndex(x => new { x.DocumentId, x.Generation, x.Ordinal }).IsUnique();
        b.Entity<DocumentChunk>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<IngestionJob>().ToTable("IngestionJobs");
        b.Entity<IngestionJob>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        b.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        b.Entity<IngestionJob>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<SupportCase>().ToTable("SupportCases");
        b.Entity<SupportCase>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<CaseAttachment>().ToTable("CaseAttachments").HasKey(x => new { x.CaseId, x.DocumentId });
        b.Entity<CaseAttachment>().HasOne<SupportCase>().WithMany().HasForeignKey(x => x.CaseId);
        b.Entity<CaseAttachment>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId);
        b.Entity<Conversation>().ToTable("Conversations");
        b.Entity<Conversation>().HasIndex(x => x.CaseId).IsUnique();
        b.Entity<Conversation>().HasOne<SupportCase>().WithMany().HasForeignKey(x => x.CaseId);
        b.Entity<Message>().ToTable("Messages");
        b.Entity<Message>().HasIndex(x => new { x.ConversationId, x.RequestId }).IsUnique();
        b.Entity<Message>().HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId);
        b.Entity<Citation>().ToTable("Citations");
        b.Entity<Citation>().HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId);
        b.Entity<Citation>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Citation>().HasOne<DocumentChunk>().WithMany().HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<ActionProposal>().ToTable("ActionProposals");
        b.Entity<ActionProposal>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<ActionProposal>().Property(x => x.ArgumentsJson).HasColumnType("jsonb");
        b.Entity<ActionProposal>().HasOne<SupportCase>().WithMany().HasForeignKey(x => x.CaseId);
        b.Entity<ActionProposal>().HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId);
        b.Entity<ActionExecution>().ToTable("ActionExecutions");
        b.Entity<ActionExecution>().Property(x => x.ResultJson).HasColumnType("jsonb");
        b.Entity<ActionExecution>().HasIndex(x => x.ProposalId).IsUnique();
        b.Entity<ActionExecution>().HasIndex(x => x.IdempotencyKey).IsUnique();
        b.Entity<ActionExecution>().Property(x => x.IdempotencyKey).HasMaxLength(128);
        b.Entity<ActionExecution>().HasOne<ActionProposal>().WithMany().HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
    }
}
