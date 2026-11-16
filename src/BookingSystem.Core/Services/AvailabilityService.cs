using BookingSystem.Core.Entities;
using BookingSystem.Core.Enums;
using BookingSystem.Core.Interfaces;
using BookingSystem.Core.Models;

namespace BookingSystem.Core.Services;

/// <summary>
/// Pure domain service: computes open slots from working hours, breaks, duration, buffer
/// and existing bookings. No I/O — fully unit-testable.
/// </summary>
public sealed class AvailabilityService : IAvailabilityService
{
    public IReadOnlyList<SlotInfo> GetAvailableSlots(
        Provider provider,
        Service service,
        BookingPolicy? policy,
        DateOnly date,
        IReadOnlyList<Booking> existingBookings,
        IReadOnlyList<HeldSlot> heldSlots,
        IReadOnlyList<ProviderBreak> breaks)
    {
        var workingHours = provider.WorkingHours
            .FirstOrDefault(wh => wh.DayOfWeek == date.DayOfWeek);

        if (workingHours is null)
            return [];

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(provider.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        // Convert working-hour boundaries for this specific date (handles DST transitions).
        var startUtc = ToUtc(date, workingHours.StartTime, tz);
        var endUtc   = ToUtc(date, workingHours.EndTime,   tz);

        if (startUtc >= endUtc)
            return [];

        // Apply max-bookings-per-day: count only non-cancelled bookings within the working day.
        if (policy is not null)
        {
            int confirmedToday = existingBookings.Count(b =>
                b.Status != BookingStatus.Cancelled &&
                b.StartUtc >= startUtc &&
                b.StartUtc < endUtc);

            if (confirmedToday >= policy.MaxBookingsPerDay)
                return [];
        }

        var slotDuration   = TimeSpan.FromMinutes(service.DurationMinutes);
        var bufferDuration = TimeSpan.FromMinutes(service.BufferMinutes);

        // Active (non-cancelled) bookings relevant to this day — extended by buffer for the
        // "tail-gap" check: a booked slot at T blocks the slot at T-buffer as well.
        var activeBookings = existingBookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToList();

        var result = new List<SlotInfo>();
        var current = startUtc;

        while (current + slotDuration <= endUtc)
        {
            var slotEnd            = current + slotDuration;
            var slotEndWithBuffer  = slotEnd + bufferDuration;

            if (!HasConflict(current, slotEndWithBuffer, activeBookings, heldSlots, breaks))
            {
                result.Add(new SlotInfo(current, slotEnd, provider.Id, service.Id, provider.TimeZoneId));
            }

            current += slotDuration + bufferDuration == TimeSpan.Zero
                ? TimeSpan.FromMinutes(15)   // fallback granularity when both are 0
                : slotDuration;
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DateTime ToUtc(DateOnly date, TimeOnly localTime, TimeZoneInfo tz)
    {
        var localDt = new DateTime(
            date.Year, date.Month, date.Day,
            localTime.Hour, localTime.Minute, 0,
            DateTimeKind.Unspecified);

        // ConvertTimeToUtc respects DST for the specific date.
        return TimeZoneInfo.ConvertTimeToUtc(localDt, tz);
    }

    private static bool HasConflict(
        DateTime slotStart,
        DateTime slotEndWithBuffer,
        IReadOnlyList<Booking> bookings,
        IReadOnlyList<HeldSlot> heldSlots,
        IReadOnlyList<ProviderBreak> breaks)
    {
        // A conflict exists when any existing interval overlaps [slotStart, slotEndWithBuffer).
        // We also respect the buffer tail of an existing booking: if booking ends at T, the
        // next slot can start no earlier than T + buffer; this is captured because we extend
        // slotEnd by buffer before the check.

        foreach (var b in bookings)
        {
            // existing booking occupies [b.StartUtc, b.EndUtc)
            // new slot (with its buffer) occupies [slotStart, slotEndWithBuffer)
            // overlap when b.StartUtc < slotEndWithBuffer AND b.EndUtc > slotStart
            if (b.StartUtc < slotEndWithBuffer && b.EndUtc > slotStart)
                return true;
        }

        foreach (var h in heldSlots)
        {
            if (h.StartUtc < slotEndWithBuffer && h.EndUtc > slotStart)
                return true;
        }

        foreach (var br in breaks)
        {
            // A break blocks a slot even if it only partially overlaps.
            if (br.StartUtc < slotEndWithBuffer && br.EndUtc > slotStart)
                return true;
        }

        return false;
    }
}
