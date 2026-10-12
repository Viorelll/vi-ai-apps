using ImageAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImageAI.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ImageProject> Projects => Set<ImageProject>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ImageRun> Runs => Set<ImageRun>();
    public DbSet<ImageAsset> Assets => Set<ImageAsset>();
    public DbSet<GenerationJob> Jobs => Set<GenerationJob>();
    public DbSet<EditMask> Masks => Set<EditMask>();
    public DbSet<GenerationSnapshot> Snapshots => Set<GenerationSnapshot>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("app");
        b.Entity<ImageProject>().ToTable("ImageProjects");
        b.Entity<ImageProject>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<Document>().ToTable("Documents");
        b.Entity<Document>().HasIndex(x => x.Sha256).IsUnique();
        b.Entity<ImageRun>().ToTable("ImageRuns");
        b.Entity<ImageRun>().HasIndex(x => x.RequestId).IsUnique();
        b.Entity<ImageRun>()
            .HasOne<ImageProject>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ImageRun>()
            .HasOne<ImageAsset>()
            .WithMany()
            .HasForeignKey(x => x.InputAssetId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ImageRun>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.InputDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ImageRun>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.MaskDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ImageAsset>().ToTable("ImageAssets");
        b.Entity<ImageAsset>().HasIndex(x => x.RunId).IsUnique();
        b.Entity<ImageAsset>()
            .HasOne<ImageProject>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ImageAsset>()
            .HasOne<ImageRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ImageAsset>()
            .HasOne<ImageAsset>()
            .WithMany()
            .HasForeignKey(x => x.ParentAssetId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<GenerationJob>().ToTable("GenerationJobs");
        b.Entity<GenerationJob>().HasIndex(x => x.RunId).IsUnique();
        b.Entity<GenerationJob>()
            .HasOne<ImageRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EditMask>().ToTable("EditMasks");
        b.Entity<EditMask>().HasIndex(x => new { x.DocumentId, x.InputAssetId }).IsUnique();
        b.Entity<EditMask>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EditMask>()
            .HasOne<ImageAsset>()
            .WithMany()
            .HasForeignKey(x => x.InputAssetId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<GenerationSnapshot>().ToTable("GenerationSnapshots");
        b.Entity<GenerationSnapshot>().HasIndex(x => x.RunId).IsUnique();
        b.Entity<GenerationSnapshot>().Property(x => x.ParametersJson).HasColumnType("jsonb");
        b.Entity<GenerationSnapshot>().Property(x => x.UsageJson).HasColumnType("jsonb");
        b.Entity<GenerationSnapshot>()
            .HasOne<ImageRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
