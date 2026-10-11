using DocuAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocuAI.Database.Persistence;

public sealed class DocuAiDbContext(DbContextOptions<DocuAiDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> Jobs => Set<IngestionJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.HasIndex(document => document.Sha256).IsUnique();
            entity.Property(document => document.FileName).HasMaxLength(240);
            entity.Property(document => document.Sha256).HasMaxLength(64);
            entity.Property(document => document.Status).HasConversion<string>().HasMaxLength(24);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(chunk => chunk.Id);
            entity.HasIndex(chunk => new { chunk.DocumentId, chunk.Generation, chunk.Ordinal }).IsUnique();
            entity.HasOne(chunk => chunk.Document)
                .WithMany(document => document.Chunks)
                .HasForeignKey(chunk => chunk.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IngestionJob>(entity =>
        {
            entity.HasKey(job => job.Id);
            entity.Property(job => job.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(job => new { job.Status, job.NextAttemptAtUtc });
        });
    }
}
