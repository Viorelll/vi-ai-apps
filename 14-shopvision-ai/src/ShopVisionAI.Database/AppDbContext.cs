using Microsoft.EntityFrameworkCore;
using ShopVisionAI.Domain.Entities;

namespace ShopVisionAI.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<CatalogProduct> CatalogProducts => Set<CatalogProduct>();
    public DbSet<VisualSearch> VisualSearches => Set<VisualSearch>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("app");
        b.Entity<Document>().ToTable("Documents").HasIndex(x => x.Sha256).IsUnique();
        b.Entity<CatalogProduct>().ToTable("CatalogProducts").HasIndex(x => x.Sku).IsUnique();
        b.Entity<CatalogProduct>().Property(x => x.AttributesJson).HasColumnType("jsonb");
        b.Entity<CatalogProduct>().Property(x => x.Price).HasPrecision(18, 2);
        b.Entity<VisualSearch>()
            .ToTable("VisualSearches")
            .Property(x => x.Revision)
            .IsConcurrencyToken();
        b.Entity<VisualSearch>().Property(x => x.ResultJson).HasColumnType("jsonb");
        b.Entity<VisualSearch>()
            .HasOne(x => x.ImageDocument)
            .WithMany()
            .HasForeignKey(x => x.ImageDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<IngestionJob>()
            .ToTable("IngestionJobs")
            .HasIndex(x => new { x.TargetId, x.RequestId })
            .IsUnique();
        b.Entity<IngestionJob>().HasOne(x => x.Target).WithMany().HasForeignKey(x => x.TargetId);
        b.Entity<ConversationMessage>()
            .ToTable("ConversationMessages")
            .HasIndex(x => new { x.SearchId, x.RequestId })
            .IsUnique();
        b.Entity<ConversationMessage>()
            .HasOne(x => x.Search)
            .WithMany()
            .HasForeignKey(x => x.SearchId);
        b.Entity<CatalogProduct>().HasData(CatalogSeed.Products);
    }
}
