using EvalAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace EvalAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<EvalDataset> EvalDatasets => Set<EvalDataset>();
    public DbSet<EvalCase> EvalCases => Set<EvalCase>();
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();
    public DbSet<EvalCaseResult> EvalCaseResults => Set<EvalCaseResult>();
    public DbSet<CostRate> CostRates => Set<CostRate>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Citation> Citations => Set<Citation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        foreach (var t in new[] { typeof(Document), typeof(DocumentChunk), typeof(IngestionJob), typeof(EvalDataset), typeof(EvalCase), typeof(EvalRun), typeof(EvalCaseResult), typeof(CostRate), typeof(Conversation), typeof(Message), typeof(Citation) })
        {
            var e = model.Entity(t); e.ToTable(t.Name + "s"); e.HasKey("Id");
            foreach (var p in e.Metadata.GetProperties().Where(p => p.ClrType == typeof(string)))
            {
                if (p.Name.EndsWith("Json")) p.SetColumnType("jsonb"); else p.SetMaxLength(p.Name == "Text" || p.Name == "Excerpt" ? 100000 : 2000);
            }
        }
        model.Entity<Document>().HasIndex(x => x.Sha256).IsUnique().HasFilter("\"Status\" <> 'Deleting'");
        model.Entity<DocumentChunk>().HasIndex(x => new { x.DocumentId, x.Generation, x.Ordinal }).IsUnique();
        model.Entity<DocumentChunk>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<IngestionJob>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        model.Entity<IngestionJob>().Property(x => x.LeaseToken).IsConcurrencyToken();
        model.Entity<IngestionJob>().HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        model.Entity<EvalDataset>().HasIndex(x => new { x.Name, x.Version }).IsUnique();
        model.Entity<EvalDataset>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<EvalCase>().HasOne<EvalDataset>().WithMany().HasForeignKey(x => x.DatasetId);
        model.Entity<EvalCase>().HasIndex(x => new { x.DatasetId, x.Ordinal }).IsUnique();
        model.Entity<EvalRun>().HasOne<EvalDataset>().WithMany().HasForeignKey(x => x.DatasetId);
        model.Entity<EvalRun>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<EvalCaseResult>().HasOne<EvalRun>().WithMany().HasForeignKey(x => x.RunId);
        model.Entity<EvalCaseResult>().HasOne<EvalCase>().WithMany().HasForeignKey(x => x.CaseId);
        model.Entity<EvalCaseResult>().HasIndex(x => new { x.RunId, x.VariantId, x.CaseId }).IsUnique();
        model.Entity<CostRate>().HasIndex(x => new { x.DeploymentAlias, x.RateVersion }).IsUnique();
        model.Entity<CostRate>().Property(x => x.InputPerMillion).HasPrecision(20, 8);
        model.Entity<CostRate>().Property(x => x.OutputPerMillion).HasPrecision(20, 8);
        model.Entity<Message>().HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId);
        model.Entity<Message>().HasIndex(x => x.RequestId).IsUnique();
        model.Entity<Citation>().HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId);
        model.Entity<Citation>().HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        model.Entity<Citation>().HasOne<DocumentChunk>().WithMany().HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.SetNull);
    }
}
