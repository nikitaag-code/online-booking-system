using BookingSystem.Core.Entities;
using BookingSystem.Core.Enums;
using BookingSystem.Core.Services;
using FluentAssertions;
using Xunit;

namespace BookingSystem.Tests.Services;

public class PolicyTests
{
    private static readonly AvailabilityService Svc = new();

    private static Provider MakeProvider() => new()
    {
        Id         = 1,
        Name       = "Policy Provider",
        TimeZoneId = "UTC",
        WorkingHours = new List<WorkingHours>
        {
            new() { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) }
        }
    };

    private static Service MakeService() => new()
    {
        Id = 1, Name = "Svc", DurationMinutes = 30, BufferMinutes = 0
    };

    // ── Max bookings per day ──────────────────────────────────────────────────

    [Fact]
    public void NoSlots_WhenMaxBookingsPerDayReached()
    {
        var provider = MakeProvider();
        var service  = MakeService();
        var date     = NextMonday();

        var policy = new BookingPolicy { CancellationWindowHours = 24, MaxBookingsPerDay = 2 };

        var existingBookings = new List<Booking>
        {
            MakeBooking(date, 9, 0),
            MakeBooking(date, 9, 30)
        };

        var slots = Svc.GetAvailableSlots(provider, service, policy, date, existingBookings, [], []);

        slots.Should().BeEmpty("max 2 bookings per day already reached");
    }

    [Fact]
    public void SlotsAvailable_WhenUnderMaxBookingsPerDay()
    {
        var provider = MakeProvider();
        var service  = MakeService();
        var date     = NextMonday();

        var policy = new BookingPolicy { CancellationWindowHours = 24, MaxBookingsPerDay = 5 };

        // Only 1 booking out of 5 max
        var existingBookings = new List<Booking> { MakeBooking(date, 9, 0) };

        var slots = Svc.GetAvailableSlots(provider, service, policy, date, existingBookings, [], []);

        slots.Should().NotBeEmpty();
    }

    [Fact]
    public void CancelledBookingsDoNotCountTowardMaxPerDay()
    {
        var provider = MakeProvider();
        var service  = MakeService();
        var date     = NextMonday();

        var policy = new BookingPolicy { CancellationWindowHours = 24, MaxBookingsPerDay = 2 };

        var existingBookings = new List<Booking>
        {
            MakeBooking(date, 9, 0),
            new() { ProviderId = 1, StartUtc = date.ToDateTime(new TimeOnly(9, 30), DateTimeKind.Utc),
                    EndUtc = date.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc), Status = BookingStatus.Cancelled }
        };

        // Only 1 confirmed → still under max of 2
        var slots = Svc.GetAvailableSlots(provider, service, policy, date, existingBookings, [], []);

        slots.Should().NotBeEmpty("one cancelled booking should not count toward max");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Booking MakeBooking(DateOnly date, int hour, int minute) => new()
    {
        ProviderId = 1,
        StartUtc   = date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc),
        EndUtc     = date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc).AddMinutes(30),
        Status     = BookingStatus.Confirmed
    };

    private static DateOnly NextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        int diff  = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return diff == 0 ? today : today.AddDays(diff);
    }
}
