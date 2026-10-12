using FeedbackAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FeedbackBatch> Batches => Set<FeedbackBatch>();
    public DbSet<FeedbackItem> Items => Set<FeedbackItem>();
    public DbSet<Classification> Classifications => Set<Classification>();
    public DbSet<LabelCorrection> Corrections => Set<LabelCorrection>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        model.Entity<FeedbackBatch>().ToTable("FeedbackBatches").HasKey(x => x.Id);
        model.Entity<FeedbackBatch>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<FeedbackBatch>().Property(x => x.TaxonomyVersion).HasMaxLength(40);
        model.Entity<FeedbackBatch>().Property(x => x.State).HasMaxLength(30);
        model.Entity<FeedbackItem>().ToTable("FeedbackItems").HasKey(x => x.Id);
        model.Entity<FeedbackItem>().HasIndex(x => new { x.BatchId, x.Ordinal }).IsUnique();
        model.Entity<FeedbackItem>().HasOne(x => x.Batch).WithMany(x => x.Items).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<FeedbackItem>().Property(x => x.OriginalText).HasMaxLength(12000);
        model.Entity<FeedbackItem>().Property(x => x.RedactedText).HasMaxLength(12000);
        model.Entity<FeedbackItem>().Property(x => x.ActiveTopic).HasMaxLength(30);
        model.Entity<FeedbackItem>().Property(x => x.ActiveUrgency).HasMaxLength(30);
        model.Entity<Classification>().ToTable("Classifications").HasKey(x => x.Id);
        model.Entity<Classification>().HasOne(x => x.Item).WithOne(x => x.Classification).HasForeignKey<Classification>(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<LabelCorrection>().ToTable("LabelCorrections").HasKey(x => x.Id);
        model.Entity<LabelCorrection>().HasIndex(x => x.ItemId);
    }
}
