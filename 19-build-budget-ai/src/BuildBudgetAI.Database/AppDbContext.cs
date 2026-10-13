using BuildBudgetAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuildBudgetAI.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BuildPlan> Plans => Set<BuildPlan>();
    public DbSet<MaterialNeed> Needs => Set<MaterialNeed>();
    public DbSet<MaterialOffer> Offers => Set<MaterialOffer>();
    public DbSet<OfferLine> OfferLines => Set<OfferLine>();
    public DbSet<PlanSource> Sources => Set<PlanSource>();
    public DbSet<BudgetSnapshot> Budgets => Set<BudgetSnapshot>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> Jobs => Set<IngestionJob>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("app");
        m.Entity<BuildPlan>().ToTable("BuildPlans");
        m.Entity<MaterialNeed>().ToTable("MaterialNeeds");
        m.Entity<MaterialOffer>().ToTable("MaterialOffers");
        m.Entity<OfferLine>().ToTable("OfferLines");
        m.Entity<PlanSource>().ToTable("PlanSources");
        m.Entity<BudgetSnapshot>().ToTable("BudgetSnapshots");
        m.Entity<Document>().ToTable("Documents");
        m.Entity<DocumentChunk>().ToTable("DocumentChunks");
        m.Entity<IngestionJob>().ToTable("IngestionJobs");
        m.Entity<Conversation>().ToTable("Conversations");
        m.Entity<Message>().ToTable("Messages");
        m.Entity<Citation>().ToTable("Citations");
        m.Entity<BuildPlan>().Property(x => x.Revision).IsConcurrencyToken();
        m.Entity<BuildPlan>()
            .HasMany(x => x.Needs)
            .WithOne()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<BuildPlan>()
            .HasMany(x => x.Offers)
            .WithOne()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<BuildPlan>()
            .HasMany(x => x.Sources)
            .WithOne()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<MaterialOffer>()
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<MaterialOffer>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
        m.Entity<PlanSource>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<PlanSource>().HasIndex(x => new { x.PlanId, x.DocumentId }).IsUnique();
        m.Entity<BudgetSnapshot>().HasOne<BuildPlan>().WithMany().HasForeignKey(x => x.PlanId);
        m.Entity<BudgetSnapshot>().HasIndex(x => new { x.PlanId, x.InputsHash }).IsUnique();
        m.Entity<Document>()
            .HasIndex(x => x.Sha256)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Deleted'");
        m.Entity<Document>().Property(x => x.Sha256).HasMaxLength(64);
        m.Entity<DocumentChunk>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<DocumentChunk>()
            .HasIndex(x => new
            {
                x.DocumentId,
                x.Generation,
                x.Ordinal,
            })
            .IsUnique();
        m.Entity<IngestionJob>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        m.Entity<Conversation>().HasOne<BuildPlan>().WithMany().HasForeignKey(x => x.PlanId);
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
        foreach (var e in m.Model.GetEntityTypes())
            foreach (var p in e.GetProperties())
            {
                if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
                {
                    p.SetPrecision(18);
                    p.SetScale(6);
                }
                if (p.ClrType == typeof(string) && p.GetMaxLength() == null)
                    p.SetMaxLength(200000);
            }
    }
}
