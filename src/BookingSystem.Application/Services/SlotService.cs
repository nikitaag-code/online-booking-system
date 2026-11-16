using BookingSystem.Application.DTOs;
using BookingSystem.Application.Exceptions;
using BookingSystem.Application.Services.Interfaces;
using BookingSystem.Core.Entities;
using BookingSystem.Core.Interfaces;
using BookingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Application.Services;

public class SlotService : ISlotService
{
    private readonly ApplicationDbContext _db;
    private readonly IAvailabilityService _availability;

    public SlotService(ApplicationDbContext db, IAvailabilityService availability)
    {
        _db           = db;
        _availability = availability;
    }

    public async Task<AvailableSlotsResponseDto> GetAvailableSlotsAsync(
        int providerId, int serviceId, DateOnly date, CancellationToken ct = default)
    {
        if (date < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            throw new ValidationException("Cannot query slots for a past date.");

        var provider = await _db.Providers
            .Where(p => p.Id == providerId && p.IsActive)
            .Include(p => p.WorkingHours)
            .Include(p => p.Policy)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (provider is null) throw new NotFoundException("Provider not found.");

        var ps = await _db.ProviderServices
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.ProviderId == providerId && x.ServiceId == serviceId, ct);

        if (ps is null) throw new NotFoundException("Service not offered by this provider.");

        var tz          = GetTz(provider.TimeZoneId);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified), tz);
        var dayEndUtc   = dayStartUtc.AddDays(1);

        var existingBookings = await _db.Bookings
            .Where(b => b.ProviderId == providerId &&
                        b.StartUtc   >= dayStartUtc &&
                        b.StartUtc   <  dayEndUtc)
            .AsNoTracking().ToListAsync(ct);

        var heldSlots = await _db.HeldSlots
            .Where(h => h.ProviderId == providerId &&
                        h.ExpiresAt  >  DateTime.UtcNow &&
                        h.StartUtc   >= dayStartUtc &&
                        h.StartUtc   <  dayEndUtc)
            .AsNoTracking().ToListAsync(ct);

        var breaks = await _db.Breaks
            .Where(b => b.ProviderId == providerId &&
                        b.StartUtc   <  dayEndUtc &&
                        b.EndUtc     >  dayStartUtc)
            .AsNoTracking().ToListAsync(ct);

        var slots = _availability.GetAvailableSlots(
            provider, ps.Service, provider.Policy, date, existingBookings, heldSlots, breaks);

        var slotDtos = slots
            .Select(s => new SlotDto(s.StartUtc, s.EndUtc, s.ProviderId, s.ServiceId, s.ProviderTimeZoneId))
            .ToList();

        return new AvailableSlotsResponseDto(date, providerId, serviceId, provider.TimeZoneId, slotDtos);
    }

    public async Task<HoldSlotResponseDto> HoldSlotAsync(HoldSlotDto dto, CancellationToken ct = default)
    {
        var ps = await _db.ProviderServices
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.ProviderId == dto.ProviderId && x.ServiceId == dto.ServiceId, ct);

        if (ps is null) throw new NotFoundException("Provider / service combination not found.");

        bool conflict = await _db.HeldSlots.AnyAsync(h =>
                            h.ProviderId == dto.ProviderId &&
                            h.StartUtc   == dto.StartUtc  &&
                            h.ExpiresAt  >  DateTime.UtcNow, ct)
                        || await _db.Bookings.AnyAsync(b =>
                            b.ProviderId == dto.ProviderId &&
                            b.StartUtc   == dto.StartUtc  &&
                            b.Status     != Core.Enums.BookingStatus.Cancelled, ct);

        if (conflict) throw new ConflictException("Slot is already held or booked.");

        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var token     = Guid.NewGuid().ToString("N");

        _db.HeldSlots.Add(new HeldSlot
        {
            ProviderId   = dto.ProviderId,
            ServiceId    = dto.ServiceId,
            StartUtc     = dto.StartUtc,
            EndUtc       = dto.StartUtc.AddMinutes(ps.Service.DurationMinutes),
            ExpiresAt    = expiresAt,
            SessionToken = token
        });

        await _db.SaveChangesAsync(ct);
        return new HoldSlotResponseDto(token, expiresAt);
    }

    public async Task ReleaseHoldAsync(string sessionToken, CancellationToken ct = default)
    {
        var held = await _db.HeldSlots
            .FirstOrDefaultAsync(h => h.SessionToken == sessionToken, ct);

        if (held is null) throw new NotFoundException("Hold not found.");
        _db.HeldSlots.Remove(held);
        await _db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo GetTz(string id)
    {
        try   { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Utc; }
    }
}
