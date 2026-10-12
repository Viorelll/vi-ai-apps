using System.Text.Json;
using MeetingAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace MeetingAI.Database;

public sealed class MeetingDbContext(DbContextOptions<MeetingDbContext> options) : DbContext(options)
{
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        var meetings = model.Entity<Meeting>(); meetings.HasKey(x => x.Id); meetings.Property(x => x.Sha256).HasMaxLength(64); meetings.HasIndex(x => x.Sha256).IsUnique().HasFilter("\"Sha256\" IS NOT NULL"); meetings.Property(x => x.Title).HasMaxLength(240); meetings.Property(x => x.Revision).IsConcurrencyToken();
        meetings.HasMany(x => x.Segments).WithOne().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        meetings.HasMany(x => x.Decisions).WithOne().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        meetings.HasMany(x => x.ActionItems).WithOne().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<TranscriptSegment>().HasKey(x => x.Id); model.Entity<TranscriptSegment>().HasIndex(x => new { x.MeetingId, x.Ordinal }).IsUnique();
        var converter = new ValueConverter<List<Guid>, string>(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new());
        var comparer = new ValueComparer<List<Guid>>((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList());
        foreach (var type in new[] { typeof(Decision), typeof(ActionItem) }) { var entity = model.Entity(type); entity.HasKey(nameof(Decision.Id)); entity.Property(nameof(Decision.SegmentIds)).HasConversion(converter, comparer).HasColumnType("jsonb"); }
    }
}
