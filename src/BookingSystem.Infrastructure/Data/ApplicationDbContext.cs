using BookingSystem.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Provider>       Providers       => Set<Provider>();
    public DbSet<Service>        Services        => Set<Service>();
    public DbSet<ProviderService> ProviderServices => Set<ProviderService>();
    public DbSet<WorkingHours>   WorkingHours    => Set<WorkingHours>();
    public DbSet<ProviderBreak>  Breaks          => Set<ProviderBreak>();
    public DbSet<Booking>        Bookings        => Set<Booking>();
    public DbSet<BookingPolicy>  BookingPolicies => Set<BookingPolicy>();
    public DbSet<HeldSlot>       HeldSlots       => Set<HeldSlot>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Provider ──────────────────────────────────────────────────────────
        builder.Entity<Provider>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.User)
             .WithOne(u => u.Provider)
             .HasForeignKey<Provider>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProviderService (many-to-many link) ───────────────────────────────
        builder.Entity<ProviderService>(e =>
        {
            e.HasKey(ps => new { ps.ProviderId, ps.ServiceId });
            e.Property(ps => ps.Price).HasColumnType("decimal(10,2)");
        });

        // ── WorkingHours ──────────────────────────────────────────────────────
        builder.Entity<WorkingHours>(e =>
        {
            e.HasKey(wh => wh.Id);
            e.HasOne(wh => wh.Provider)
             .WithMany(p => p.WorkingHours)
             .HasForeignKey(wh => wh.ProviderId)
             .OnDelete(DeleteBehavior.Cascade);

            // TimeOnly stored as time(7)
            e.Property(wh => wh.StartTime).HasColumnType("time");
            e.Property(wh => wh.EndTime).HasColumnType("time");
        });

        // ── ProviderBreak ─────────────────────────────────────────────────────
        builder.Entity<ProviderBreak>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasOne(b => b.Provider)
             .WithMany(p => p.Breaks)
             .HasForeignKey(b => b.ProviderId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(b => b.StartUtc).HasColumnType("datetime2");
            e.Property(b => b.EndUtc).HasColumnType("datetime2");
        });

        // ── Booking ───────────────────────────────────────────────────────────
        builder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);

            // THE concurrency guard: a provider cannot have two confirmed bookings
            // at the same UTC start time. The DB enforces this even under concurrent
            // requests — no check-then-insert race condition.
            e.HasIndex(b => new { b.ProviderId, b.StartUtc })
             .IsUnique()
             .HasDatabaseName("UX_Booking_Provider_StartUtc");

            e.Property(b => b.StartUtc).HasColumnType("datetime2");
            e.Property(b => b.EndUtc).HasColumnType("datetime2");
            e.Property(b => b.CreatedAt).HasColumnType("datetime2");
            e.Property(b => b.UpdatedAt).HasColumnType("datetime2");
            e.Property(b => b.Status).HasConversion<int>();

            e.HasOne(b => b.Customer)
             .WithMany(u => u.Bookings)
             .HasForeignKey(b => b.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Provider)
             .WithMany(p => p.Bookings)
             .HasForeignKey(b => b.ProviderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Service)
             .WithMany()
             .HasForeignKey(b => b.ServiceId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── BookingPolicy ─────────────────────────────────────────────────────
        builder.Entity<BookingPolicy>(e =>
        {
            e.HasKey(bp => bp.Id);
            e.HasOne(bp => bp.Provider)
             .WithOne(p => p.Policy)
             .HasForeignKey<BookingPolicy>(bp => bp.ProviderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── HeldSlot ──────────────────────────────────────────────────────────
        builder.Entity<HeldSlot>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.StartUtc).HasColumnType("datetime2");
            e.Property(h => h.EndUtc).HasColumnType("datetime2");
            e.Property(h => h.ExpiresAt).HasColumnType("datetime2");

            e.HasIndex(h => new { h.ProviderId, h.StartUtc })
             .HasDatabaseName("IX_HeldSlot_Provider_StartUtc");

            e.HasIndex(h => h.ExpiresAt)
             .HasDatabaseName("IX_HeldSlot_ExpiresAt");
        });
    }
}
