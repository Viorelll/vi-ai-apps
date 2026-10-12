using System.Text.Json;
using IncidentAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IncidentAI.Infrastructure.Persistence;

public sealed class IncidentDbContext(DbContextOptions<IncidentDbContext> options) : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<LogEvent> LogEvents => Set<LogEvent>();
    public DbSet<DiagnosisRun> DiagnosisRuns => Set<DiagnosisRun>();
    public DbSet<ReleaseSnapshot> Releases => Set<ReleaseSnapshot>();
    public DbSet<LogSource> LogSources => Set<LogSource>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        model.Entity<Incident>(e =>
        {
            e.ToTable("incidents"); e.HasKey(x => x.Id); e.Property(x => x.Service).HasMaxLength(160).IsRequired();
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Diagnoses).WithOne().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Releases).WithOne().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.LogSources).WithOne().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Service, x.FromUtc, x.ToUtc });
        });
        model.Entity<LogEvent>(e =>
        {
            e.ToTable("log_events"); e.HasKey(x => x.Id); e.Property(x => x.SourceId).HasMaxLength(64);
            e.Property(x => x.Severity).HasMaxLength(24); e.Property(x => x.MessageRedacted).HasMaxLength(8192);
            e.HasIndex(x => new { x.IncidentId, x.TimestampUtc, x.TraceId });
        });
        model.Entity<DiagnosisRun>(e =>
        {
            e.ToTable("diagnosis_runs"); e.HasKey(x => x.Id); e.Property(x => x.State).HasMaxLength(32);
            e.Property(x => x.Summary).HasMaxLength(12000);
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            e.Property(x => x.Hypotheses).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<Hypothesis>>(v, jsonOptions) ?? new List<Hypothesis>(),
                new ValueComparer<List<Hypothesis>>((a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions), v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(), v => JsonSerializer.Deserialize<List<Hypothesis>>(JsonSerializer.Serialize(v, jsonOptions), jsonOptions)!));
            e.Property(x => x.NextChecks).HasColumnType("jsonb").HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>(),
                new ValueComparer<List<string>>((a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions), v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(), v => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, jsonOptions), jsonOptions)!));
        });
        model.Entity<ReleaseSnapshot>(e => { e.ToTable("release_snapshots"); e.HasKey(x => x.Id); e.Property(x => x.Label).HasMaxLength(160); });
        model.Entity<LogSource>(e => { e.ToTable("log_sources"); e.HasKey(x => x.Id); e.Property(x => x.FileName).HasMaxLength(255); e.Property(x => x.ContentType).HasMaxLength(128); e.Property(x => x.Sha256).HasMaxLength(64); e.Property(x => x.ObjectKey).HasMaxLength(512); e.HasIndex(x => x.Sha256); });
    }
}
