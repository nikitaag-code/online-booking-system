using BookingSystem.Core.Entities;
using BookingSystem.Core.Enums;
using BookingSystem.Core.Services;
using FluentAssertions;
using Xunit;

namespace BookingSystem.Tests.Services;

public class AvailabilityServiceTests
{
    private static readonly AvailabilityService Svc = new();

    private static Provider MakeProvider(string tz = "UTC") => new()
    {
        Id         = 1,
        Name       = "Dr Test",
        TimeZoneId = tz,
        WorkingHours = new List<WorkingHours>
        {
            new() { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) }
        }
    };

    private static Service MakeService(int duration = 30, int buffer = 0) => new()
    {
        Id              = 1,
        Name            = "Test Service",
        DurationMinutes = duration,
        BufferMinutes   = buffer
    };

    // ── Slot generation ───────────────────────────────────────────────────────

    [Fact]
    public void GeneratesCorrectSlots_ForEmptyDay()
    {
        var provider = MakeProvider();
        var service  = MakeService(30, 0);
        var date     = NextMonday();

        var slots = Svc.GetAvailableSlots(provider, service, null, date, [], [], []);

        // 9:00 to 17:00 = 8 hours = 16 × 30-min slots
        slots.Should().HaveCount(16);
        slots.First().StartUtc.Hour.Should().Be(9);
        slots.Last().EndUtc.Hour.Should().Be(17);
    }

    [Fact]
    public void NoSlots_WhenNoWorkingHours()
    {
        var provider = MakeProvider();
        var service  = MakeService(30);
        // Pick a Sunday — no working hours defined
        var sunday   = NextDayOfWeek(DayOfWeek.Sunday);

        var slots = Svc.GetAvailableSlots(provider, service, null, sunday, [], [], []);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void ExcludesBookedSlots()
    {
        var provider = MakeProvider();
        var service  = MakeService(30, 0);
        var date     = NextMonday();

        // Book 9:00–9:30
        var existingBookings = new List<Booking>
        {
            new()
            {
                ProviderId = 1,
                StartUtc   = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
                EndUtc     = date.ToDateTime(new TimeOnly(9, 30), DateTimeKind.Utc),
                Status     = BookingStatus.Confirmed
            }
        };

        var slots = Svc.GetAvailableSlots(provider, service, null, date, existingBookings, [], []);

        slots.Should().HaveCount(15);
        slots.Should().NotContain(s => s.StartUtc.Hour == 9 && s.StartUtc.Minute == 0);
    }

    [Fact]
    public void RespectsBufferTime()
    {
        var provider = MakeProvider();
        // 30-min service with 10-min buffer
        var service  = MakeService(30, 10);
        var date     = NextMonday();

        // Book 9:00–9:30 (with 10-min buffer → occupied until 9:40)
        var existingBookings = new List<Booking>
        {
            new()
            {
                ProviderId = 1,
                StartUtc   = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
                EndUtc     = date.ToDateTime(new TimeOnly(9, 30), DateTimeKind.Utc),
                Status     = BookingStatus.Confirmed
            }
        };

        var slots = Svc.GetAvailableSlots(provider, service, null, date, existingBookings, [], []);

        // 9:00 blocked (booked), 9:30 would end at 10:00 + buffer = 10:10
        // but 9:30 start < existing EndUtc(9:30)+buffer(10 min) = 9:40 → conflict
        slots.Should().NotContain(s => s.StartUtc.Hour == 9 && s.StartUtc.Minute == 30);
        // 10:00 is the first available slot after the buffer
        slots.Should().Contain(s => s.StartUtc.Hour == 10 && s.StartUtc.Minute == 0);
    }

    [Fact]
    public void ExcludesBreaks()
    {
        var provider = MakeProvider();
        var service  = MakeService(30, 0);
        var date     = NextMonday();

        // 12:00–13:00 break
        var breaks = new List<ProviderBreak>
        {
            new()
            {
                ProviderId = 1,
                StartUtc   = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
                EndUtc     = date.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Utc),
                Name       = "Lunch"
            }
        };

        var slots = Svc.GetAvailableSlots(provider, service, null, date, [], [], breaks);

        // 12:00 and 12:30 should be excluded (they overlap the break)
        slots.Should().NotContain(s => s.StartUtc.Hour == 12 && s.StartUtc.Minute == 0);
        slots.Should().NotContain(s => s.StartUtc.Hour == 12 && s.StartUtc.Minute == 30);
        slots.Should().Contain(s => s.StartUtc.Hour == 13 && s.StartUtc.Minute == 0);
    }

    [Fact]
    public void ExcludesHeldSlots()
    {
        var provider = MakeProvider();
        var service  = MakeService(30, 0);
        var date     = NextMonday();

        var heldSlots = new List<HeldSlot>
        {
            new()
            {
                ProviderId = 1,
                StartUtc   = date.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc),
                EndUtc     = date.ToDateTime(new TimeOnly(10, 30), DateTimeKind.Utc),
                ExpiresAt  = DateTime.UtcNow.AddMinutes(5)
            }
        };

        var slots = Svc.GetAvailableSlots(provider, service, null, date, [], heldSlots, []);

        slots.Should().NotContain(s => s.StartUtc.Hour == 10 && s.StartUtc.Minute == 0);
    }

    [Fact]
    public void CancelledBookingDoesNotBlockSlot()
    {
        var provider = MakeProvider();
        var service  = MakeService(30, 0);
        var date     = NextMonday();

        var cancelledBookings = new List<Booking>
        {
            new()
            {
                ProviderId = 1,
                StartUtc   = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
                EndUtc     = date.ToDateTime(new TimeOnly(9, 30), DateTimeKind.Utc),
                Status     = BookingStatus.Cancelled
            }
        };

        var slots = Svc.GetAvailableSlots(provider, service, null, date, cancelledBookings, [], []);

        // 9:00 slot should be available again
        slots.Should().Contain(s => s.StartUtc.Hour == 9 && s.StartUtc.Minute == 0);
    }

    [Fact]
    public void SlotContainsCorrectProviderAndServiceIds()
    {
        var provider = MakeProvider();
        var service  = MakeService(30);
        var date     = NextMonday();

        var slots = Svc.GetAvailableSlots(provider, service, null, date, [], [], []);

        slots.Should().AllSatisfy(s =>
        {
            s.ProviderId.Should().Be(1);
            s.ServiceId.Should().Be(1);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateOnly NextMonday() => NextDayOfWeek(DayOfWeek.Monday);

    private static DateOnly NextDayOfWeek(DayOfWeek target)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)); // at least tomorrow
        int diff  = ((int)target - (int)today.DayOfWeek + 7) % 7;
        return diff == 0 ? today : today.AddDays(diff);
    }
}
