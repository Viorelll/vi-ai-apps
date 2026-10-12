using DataAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<SalesRow> SalesRows => Set<SalesRow>();
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        model.Entity<Dataset>(b =>
        {
            b.ToTable("Datasets"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.SchemaVersion).HasMaxLength(40); b.Property(x => x.Revision).IsConcurrencyToken();
            b.Property(x => x.ObjectKey).HasMaxLength(300);
            b.HasMany(x => x.Rows).WithOne().HasForeignKey(x => x.DatasetId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<SalesRow>(b =>
        {
            b.ToTable("SalesRows"); b.HasKey(x => x.Id); b.Property(x => x.Category).HasMaxLength(100).IsRequired();
            b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength(); b.Property(x => x.Quantity).HasPrecision(18, 6);
            b.Property(x => x.UnitPrice).HasPrecision(18, 4); b.HasIndex(x => new { x.DatasetId, x.SoldOn });
        });
        model.Entity<AnalysisRun>(b =>
        {
            b.ToTable("AnalysisRuns"); b.HasKey(x => x.Id); b.Property(x => x.Question).HasMaxLength(2000).IsRequired();
            b.Property(x => x.ResultHash).HasMaxLength(64).IsRequired(); b.HasIndex(x => new { x.DatasetId, x.CreatedAtUtc });
        });
    }
}
