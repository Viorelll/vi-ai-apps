using Microsoft.EntityFrameworkCore;
using VoiceAI.Domain.Entities;

namespace VoiceAI.Database.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<VoiceSession> VoiceSessions => Set<VoiceSession>();
    public DbSet<VoiceTurn> VoiceTurns => Set<VoiceTurn>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<BookingProposal> BookingProposals => Set<BookingProposal>();
    public DbSet<LocalBooking> LocalBookings => Set<LocalBooking>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("app");
        model.Entity<VoiceSession>().ToTable("voice_sessions").HasKey(x => x.Id);
        model.Entity<VoiceSession>().Property(x => x.TimeZoneId).HasMaxLength(100);
        model.Entity<VoiceSession>().Property(x => x.State).HasMaxLength(30);
        model.Entity<VoiceTurn>().ToTable("voice_turns").HasKey(x => x.Id);
        model.Entity<VoiceTurn>().HasIndex(x => new { x.SessionId, x.RequestId }).IsUnique();
        model.Entity<VoiceTurn>().Property(x => x.RequestId).HasMaxLength(100);
        model.Entity<VoiceTurn>().Property(x => x.InputAudioKey).HasMaxLength(500);
        model.Entity<VoiceTurn>().Property(x => x.OutputAudioKey).HasMaxLength(500);
        model.Entity<AvailabilitySlot>().ToTable("availability_slots").HasKey(x => x.Id);
        model.Entity<AvailabilitySlot>().Property(x => x.ResourceLabel).HasMaxLength(100);
        model.Entity<AvailabilitySlot>().HasIndex(x => x.StartUtc);
        model.Entity<BookingProposal>().ToTable("booking_proposals").HasKey(x => x.Id);
        model.Entity<BookingProposal>().Property(x => x.State).HasMaxLength(30);
        model.Entity<LocalBooking>().ToTable("local_bookings").HasKey(x => x.Id);
        model.Entity<LocalBooking>().HasIndex(x => x.SlotId).IsUnique();
        model.Entity<LocalBooking>().HasIndex(x => x.ProposalId).IsUnique();
        model.Entity<LocalBooking>().HasIndex(x => x.IdempotencyKey).IsUnique();
        model.Entity<LocalBooking>().Property(x => x.IdempotencyKey).HasMaxLength(100);
        model
            .Entity<LocalBooking>()
            .HasOne(x => x.Slot)
            .WithOne(x => x.Booking)
            .HasForeignKey<LocalBooking>(x => x.SlotId);
        model
            .Entity<LocalBooking>()
            .HasOne(x => x.Proposal)
            .WithOne(x => x.Booking)
            .HasForeignKey<LocalBooking>(x => x.ProposalId);
        model.Entity<BookingProposal>().HasOne(x => x.Slot).WithMany().HasForeignKey(x => x.SlotId);
        model
            .Entity<BookingProposal>()
            .HasOne(x => x.Session)
            .WithMany(x => x.Proposals)
            .HasForeignKey(x => x.SessionId);
        model
            .Entity<BookingProposal>()
            .HasOne(x => x.VoiceTurn)
            .WithMany()
            .HasForeignKey(x => x.VoiceTurnId);
        model
            .Entity<VoiceTurn>()
            .HasOne(x => x.Session)
            .WithMany(x => x.Turns)
            .HasForeignKey(x => x.SessionId);
    }
}
