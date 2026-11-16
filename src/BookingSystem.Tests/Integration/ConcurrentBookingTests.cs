using BookingSystem.Core.Entities;
using BookingSystem.Core.Enums;
using BookingSystem.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingSystem.Tests.Integration;

/// <summary>
/// Verifies the unique index on (ProviderId, StartUtc) guarantees exactly one
/// booking succeeds when two concurrent requests target the same slot.
/// Uses an in-memory provider for speed; for a true race-condition test against
/// a real SQL Server instance, switch to a SQL Server connection string.
/// </summary>
public class ConcurrentBookingTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public ConcurrentBookingTests()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source=concurrent-test-{Guid.NewGuid():N}.db")
            .Options;

        _db = new ApplicationDbContext(opts);
        _db.Database.EnsureCreated();

        // Seed a provider and service
        _db.Providers.Add(new Provider { Id = 1, Name = "Test Provider", TimeZoneId = "UTC", UserId = "user-1" });
        _db.Services.Add(new Service   { Id = 1, Name = "Test Svc", DurationMinutes = 30 });
        _db.SaveChanges();
    }

    [Fact]
    public async Task OnlyOneBookingSucceeds_WhenTwoRequestsTargetSameSlot()
    {
        var slotStart = DateTime.UtcNow.Date.AddDays(7).AddHours(10); // future Monday 10:00 UTC
        var slotEnd   = slotStart.AddMinutes(30);

        int successCount = 0;
        int conflictCount = 0;
        var lockObj = new object();

        // Simulate two concurrent booking attempts using separate DbContext instances
        var tasks = Enumerable.Range(0, 2).Select(async _ =>
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_db.Database.GetConnectionString()!)
                .Options;

            await using var ctx = new ApplicationDbContext(opts);

            var booking = new Booking
            {
                CustomerId = $"customer-{Guid.NewGuid():N}",
                ProviderId = 1,
                ServiceId  = 1,
                StartUtc   = slotStart,
                EndUtc     = slotEnd,
                Status     = BookingStatus.Confirmed,
                CreatedAt  = DateTime.UtcNow
            };

            ctx.Bookings.Add(booking);

            try
            {
                await ctx.SaveChangesAsync();
                lock (lockObj) successCount++;
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                      ex.InnerException?.Message.Contains("unique")  == true ||
                      ex.InnerException?.Message.Contains("duplicate") == true)
            {
                lock (lockObj) conflictCount++;
            }
        }).ToList();

        await Task.WhenAll(tasks);

        successCount.Should().Be(1,  "exactly one concurrent booking should succeed");
        conflictCount.Should().Be(1, "the second concurrent request should get a conflict");

        var saved = await _db.Bookings.CountAsync();
        saved.Should().Be(1, "only one booking should be persisted");
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
