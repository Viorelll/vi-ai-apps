using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Domain.Entities;

namespace VideoSearchAI.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<VideoEntry> Videos => Set<VideoEntry>();
    public DbSet<VideoSegment> Segments => Set<VideoSegment>();
    public DbSet<FrameEvidence> Frames => Set<FrameEvidence>();
    public DbSet<IngestionJob> Jobs => Set<IngestionJob>();
    public DbSet<SearchRun> Runs => Set<SearchRun>();
    public DbSet<TemporalCitation> Citations => Set<TemporalCitation>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("app");
        m.Entity<Document>().ToTable("Documents");
        m.Entity<Document>().HasIndex(x => x.Sha256).IsUnique().HasFilter("\"Deleted\" = false");
        m.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        m.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        m.Entity<VideoEntry>().ToTable("VideoEntries");
        m.Entity<VideoEntry>().HasIndex(x => x.DocumentId).IsUnique();
        m.Entity<VideoEntry>().Property(x => x.Revision).IsConcurrencyToken();
        m.Entity<VideoEntry>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId);
        m.Entity<VideoSegment>().ToTable("VideoSegments");
        m.Entity<VideoSegment>()
            .HasIndex(x => new
            {
                x.VideoId,
                x.Generation,
                x.Ordinal,
            })
            .IsUnique();
        m.Entity<VideoSegment>()
            .HasOne<VideoEntry>()
            .WithMany()
            .HasForeignKey(x => x.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<FrameEvidence>().ToTable("FrameEvidence");
        m.Entity<FrameEvidence>()
            .HasOne<VideoSegment>()
            .WithMany()
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<IngestionJob>().ToTable("IngestionJobs");
        m.Entity<IngestionJob>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        m.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        m.Entity<IngestionJob>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
        m.Entity<SearchRun>().ToTable("SearchRuns");
        m.Entity<SearchRun>().HasIndex(x => x.RequestId).IsUnique();
        m.Entity<SearchRun>().Property(x => x.AllowedVideoIdsJson).HasColumnType("jsonb");
        m.Entity<SearchRun>().Property(x => x.ResultJson).HasColumnType("jsonb");
        m.Entity<TemporalCitation>().ToTable("TemporalCitations");
        m.Entity<TemporalCitation>()
            .HasOne<SearchRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<TemporalCitation>()
            .HasOne<VideoSegment>()
            .WithMany()
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
