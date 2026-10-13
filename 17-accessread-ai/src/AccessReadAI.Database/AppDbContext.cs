using AccessReadAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> Jobs => Set<IngestionJob>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<ReadingBlock> Blocks => Set<ReadingBlock>();
    public DbSet<TextVariant> Variants => Set<TextVariant>();
    public DbSet<NarrationSegment> Narration => Set<NarrationSegment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("app");
        m.Entity<Document>().ToTable("Documents").HasIndex(x => x.Sha256).IsUnique();
        m.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        m.Entity<Document>().Property(x => x.FileName).HasMaxLength(240);
        m.Entity<DocumentChunk>()
            .ToTable("DocumentChunks")
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
        m.Entity<DocumentChunk>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        m.Entity<IngestionJob>().ToTable("IngestionJobs");
        m.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        m.Entity<IngestionJob>().Property(x => x.InputJson).HasColumnType("jsonb");
        m.Entity<IngestionJob>().HasIndex(x => new { x.State, x.NextAttemptAtUtc });
        m.Entity<IngestionJob>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<Reading>().ToTable("Readings").Property(x => x.Revision).IsConcurrencyToken();
        m.Entity<Reading>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<Reading>().HasMany(x => x.Blocks).WithOne().HasForeignKey(x => x.ReadingId);
        m.Entity<ReadingBlock>()
            .ToTable("ReadingBlocks")
            .HasIndex(x => new { x.ReadingId, x.Ordinal })
            .IsUnique();
        m.Entity<ReadingBlock>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        m.Entity<ReadingBlock>().HasMany(x => x.Variants).WithOne().HasForeignKey(x => x.BlockId);
        m.Entity<ReadingBlock>()
            .HasMany(x => x.ImageDescriptions)
            .WithOne()
            .HasForeignKey(x => x.BlockId);
        m.Entity<TextVariant>()
            .ToTable("TextVariants")
            .HasIndex(x => new { x.BlockId, x.Kind })
            .IsUnique();
        m.Entity<TextVariant>().HasMany(x => x.Narration).WithOne().HasForeignKey(x => x.VariantId);
        m.Entity<ImageDescription>().ToTable("ImageDescriptions");
        m.Entity<ImageDescription>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        m.Entity<NarrationSegment>()
            .ToTable("NarrationSegments")
            .HasIndex(x => new
            {
                x.VariantId,
                x.TextHash,
                x.Ordinal,
            })
            .IsUnique();
        m.Entity<Conversation>()
            .ToTable("Conversations")
            .HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(x => x.ConversationId);
        m.Entity<Message>().ToTable("Messages").HasIndex(x => x.RequestId).IsUnique();
        m.Entity<Message>().HasMany(x => x.Citations).WithOne().HasForeignKey(x => x.MessageId);
        m.Entity<Citation>().ToTable("Citations");
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
