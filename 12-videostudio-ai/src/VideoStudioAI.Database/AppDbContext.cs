using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Domain.Entities;

namespace VideoStudioAI.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<VideoProject> Projects => Set<VideoProject>();
    public DbSet<GenerationJob> Jobs => Set<GenerationJob>();
    public DbSet<VideoClip> Clips => Set<VideoClip>();
    public DbSet<RemixLink> RemixLinks => Set<RemixLink>();
    public DbSet<RunSnapshot> Snapshots => Set<RunSnapshot>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("app");
        m.Entity<VideoProject>().ToTable("VideoProjects");
        m.Entity<GenerationJob>().ToTable("VideoGenerationJobs");
        m.Entity<VideoClip>().ToTable("VideoClips");
        m.Entity<RemixLink>().ToTable("RemixLinks");
        m.Entity<RunSnapshot>().ToTable("VideoRunSnapshots");
        m.Entity<Document>().ToTable("Documents");
        m.Entity<IngestionJob>().ToTable("IngestionJobs");
        m.Entity<DocumentChunk>().ToTable("DocumentChunks");
        m.Entity<VideoProject>().Property(x => x.Revision).IsConcurrencyToken();
        m.Entity<VideoProject>().Property(x => x.StoryboardJson).HasColumnType("jsonb");
        m.Entity<GenerationJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        m.Entity<GenerationJob>().Property(x => x.InputJson).HasColumnType("jsonb");
        m.Entity<GenerationJob>().HasIndex(x => new { x.ProjectId, x.RequestId }).IsUnique();
        m.Entity<GenerationJob>().HasIndex(x => new { x.State, x.NextPollAtUtc });
        m.Entity<GenerationJob>().HasOne<VideoProject>().WithMany().HasForeignKey(x => x.ProjectId);
        m.Entity<GenerationJob>()
            .HasOne<VideoClip>()
            .WithMany()
            .HasForeignKey(x => x.ParentClipId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<VideoClip>().HasIndex(x => x.JobId).IsUnique();
        m.Entity<VideoClip>()
            .HasOne<GenerationJob>()
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<VideoClip>()
            .HasOne<VideoProject>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<RemixLink>().HasIndex(x => x.ResultClipId).IsUnique();
        m.Entity<RemixLink>()
            .HasOne<VideoClip>()
            .WithMany()
            .HasForeignKey(x => x.SourceClipId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<RemixLink>()
            .HasOne<VideoClip>()
            .WithMany()
            .HasForeignKey(x => x.ResultClipId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<RunSnapshot>().HasIndex(x => x.JobId).IsUnique();
        m.Entity<RunSnapshot>().Property(x => x.ParametersJson).HasColumnType("jsonb");
        m.Entity<RunSnapshot>().HasOne<GenerationJob>().WithMany().HasForeignKey(x => x.JobId);
        m.Entity<Document>().HasIndex(x => x.Sha256).IsUnique();
        m.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        m.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        m.Entity<IngestionJob>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId);
        m.Entity<DocumentChunk>().HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique();
        m.Entity<DocumentChunk>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId);
        m.Entity<Message>().HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId);
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
