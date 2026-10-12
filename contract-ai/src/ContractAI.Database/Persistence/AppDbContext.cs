using ContractAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContractAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<ContractVersion> ContractVersions => Set<ContractVersion>();
    public DbSet<Clause> Clauses => Set<Clause>();
    public DbSet<Obligation> Obligations => Set<Obligation>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<Comparison> Comparisons => Set<Comparison>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        foreach (
            var type in new[]
            {
                typeof(Document),
                typeof(DocumentChunk),
                typeof(IngestionJob),
                typeof(ContractVersion),
                typeof(Clause),
                typeof(Obligation),
                typeof(Finding),
                typeof(Comparison),
                typeof(Conversation),
                typeof(Message),
                typeof(Citation),
            }
        )
        {
            var entity = model.Entity(type);
            entity.ToTable(
                type.Name == "DocumentChunk" ? "DocumentChunks"
                : type.Name == "ContractVersion" ? "ContractVersions"
                : type.Name == "IngestionJob" ? "IngestionJobs"
                : type.Name + "s"
            );
            entity.HasKey("Id");
            foreach (
                var property in entity
                    .Metadata.GetProperties()
                    .Where(p => p.ClrType == typeof(string))
            )
                property.SetMaxLength(100000);
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
        model
            .Entity<ContractVersion>()
            .HasIndex(x => new { x.ContractKey, x.VersionLabel })
            .IsUnique();
        model.Entity<ContractVersion>().Property(x => x.ContractKey).HasMaxLength(100);
        model.Entity<ContractVersion>().Property(x => x.VersionLabel).HasMaxLength(80);
        model.Entity<ContractVersion>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<ContractVersion>().Property(x => x.UnknownsJson).HasColumnType("jsonb");
        model
            .Entity<ContractVersion>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        model
            .Entity<ContractVersion>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.ParentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<Clause>().HasIndex(x => new { x.VersionId, x.Ordinal }).IsUnique();
        model
            .Entity<Clause>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
        model
            .Entity<Clause>()
            .HasOne<DocumentChunk>()
            .WithMany()
            .HasForeignKey(x => x.ChunkId)
            .OnDelete(DeleteBehavior.Restrict);
        model
            .Entity<Obligation>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
        model
            .Entity<Obligation>()
            .HasOne<Clause>()
            .WithMany()
            .HasForeignKey(x => x.ClauseId)
            .OnDelete(DeleteBehavior.Cascade);
        model.Entity<Obligation>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        model
            .Entity<Finding>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
        model
            .Entity<Finding>()
            .HasOne<Clause>()
            .WithMany()
            .HasForeignKey(x => x.ClauseId)
            .OnDelete(DeleteBehavior.SetNull);
        model.Entity<Finding>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        model
            .Entity<Comparison>()
            .HasIndex(x => new
            {
                x.LeftVersionId,
                x.RightVersionId,
                x.PipelineVersion,
                x.InputHash,
            })
            .IsUnique();
        model
            .Entity<Comparison>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.LeftVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        model
            .Entity<Comparison>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.RightVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<Comparison>().Property(x => x.ResultJson).HasColumnType("jsonb");
        model
            .Entity<Conversation>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
        model
            .Entity<Message>()
            .HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        model.Entity<Message>().HasIndex(x => new { x.VersionId, x.RequestId }).IsUnique();
        model
            .Entity<Message>()
            .HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
        model
            .Entity<Citation>()
            .HasOne<Message>()
            .WithMany()
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
        model
            .Entity<Citation>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
        model
            .Entity<Citation>()
            .HasOne<DocumentChunk>()
            .WithMany()
            .HasForeignKey(x => x.ChunkId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
