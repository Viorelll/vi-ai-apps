using Microsoft.EntityFrameworkCore;
using TutorAI.Domain.Entities;

namespace TutorAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<TutorSession> TutorSessions => Set<TutorSession>();
    public DbSet<LearningSource> LearningSources => Set<LearningSource>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AdaptationStep> AdaptationSteps => Set<AdaptationStep>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("app");
        m.Entity<Document>().ToTable("Documents");
        m.Entity<Document>().HasIndex(x => x.Sha256).IsUnique();
        m.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        m.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        m.Entity<DocumentChunk>().ToTable("DocumentChunks");
        m.Entity<DocumentChunk>()
            .HasIndex(x => new
            {
                x.DocumentId,
                x.Generation,
                x.Ordinal,
            })
            .IsUnique();
        m.Entity<DocumentChunk>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<IngestionJob>().ToTable("IngestionJobs");
        m.Entity<IngestionJob>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
        m.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        m.Entity<IngestionJob>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        m.Entity<TutorSession>()
            .ToTable(
                "TutorSessions",
                t => t.HasCheckConstraint("CK_Difficulty", "\"Difficulty\" BETWEEN 1 AND 3")
            );
        m.Entity<TutorSession>().Property(x => x.Revision).IsConcurrencyToken();
        m.Entity<TutorSession>().Property(x => x.ExplanationJson).HasColumnType("jsonb");
        m.Entity<LearningSource>().ToTable("LearningSources");
        m.Entity<LearningSource>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
        m.Entity<Exercise>().ToTable("Exercises");
        m.Entity<Exercise>().HasOne<TutorSession>().WithMany().HasForeignKey(x => x.SessionId);
        m.Entity<Exercise>().Property(x => x.RubricJson).HasColumnType("jsonb");
        m.Entity<Exercise>().Property(x => x.OptionsJson).HasColumnType("jsonb");
        m.Entity<Exercise>().Property(x => x.SourceIdsJson).HasColumnType("jsonb");
        m.Entity<Exercise>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        m.Entity<Attempt>()
            .ToTable(
                "Attempts",
                t =>
                    t.HasCheckConstraint(
                        "CK_Score",
                        "\"Score\" IS NULL OR (\"Score\" BETWEEN 0 AND 1)"
                    )
            );
        m.Entity<Attempt>().HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId);
        m.Entity<Attempt>().HasIndex(x => new { x.ExerciseId, x.RequestId }).IsUnique();
        // One assessment per exercise prevents repeated submissions from inflating adaptation.
        m.Entity<Attempt>().HasIndex(x => x.ExerciseId).IsUnique();
        m.Entity<Attempt>().Property(x => x.Score).HasPrecision(5, 4);
        m.Entity<Attempt>().Property(x => x.FeedbackJson).HasColumnType("jsonb");
        m.Entity<AdaptationStep>().ToTable("AdaptationSteps");
        m.Entity<AdaptationStep>()
            .HasOne<TutorSession>()
            .WithMany()
            .HasForeignKey(x => x.SessionId);
        m.Entity<AdaptationStep>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId);
        m.Entity<AdaptationStep>().HasIndex(x => x.AttemptId).IsUnique();
        m.Entity<Conversation>().ToTable("Conversations");
        m.Entity<Conversation>().HasOne<TutorSession>().WithMany().HasForeignKey(x => x.SessionId);
        m.Entity<Conversation>().HasIndex(x => x.SessionId).IsUnique();
        m.Entity<Message>().ToTable("Messages");
        m.Entity<Message>().HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId);
        m.Entity<Message>().HasIndex(x => x.OperationId).IsUnique();
        m.Entity<Citation>().ToTable("Citations");
        m.Entity<Citation>().HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId);
        m.Entity<Citation>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
        m.Entity<Citation>()
            .HasOne<DocumentChunk>()
            .WithMany()
            .HasForeignKey(x => x.ChunkId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
