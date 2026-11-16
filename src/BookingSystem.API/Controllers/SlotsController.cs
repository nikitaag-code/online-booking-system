using BookingSystem.API.DTOs;
using BookingSystem.Core.Interfaces;
using BookingSystem.Core.Models;
using BookingSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlotsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAvailabilityService _availability;

    public SlotsController(ApplicationDbContext db, IAvailabilityService availability)
    {
        _db           = db;
        _availability = availability;
    }

    /// <summary>
    /// Returns all open slots for a provider+service on a given UTC date.
    /// The date is interpreted in the provider's local timezone.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(AvailableSlotsResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSlots(
        [FromQuery] int providerId,
        [FromQuery] int serviceId,
        [FromQuery] DateOnly date)
    {
        if (date < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            return BadRequest("Cannot query slots for a past date.");

        var provider = await _db.Providers
            .Where(p => p.Id == providerId && p.IsActive)
            .Include(p => p.WorkingHours)
            .Include(p => p.Policy)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (provider is null)
            return NotFound("Provider not found.");

        // Verify this provider offers the requested service.
        var ps = await _db.ProviderServices
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.ProviderId == providerId && x.ServiceId == serviceId);

        if (ps is null)
            return NotFound("Service not offered by this provider.");

        // Load all data needed for slot generation in one pass.
        var tz            = GetTz(provider.TimeZoneId);
        var dayStartUtc   = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified), tz);
        var dayEndUtc     = dayStartUtc.AddDays(1);

        var existingBookings = await _db.Bookings
            .Where(b => b.ProviderId == providerId &&
                        b.StartUtc   >= dayStartUtc &&
                        b.StartUtc   <  dayEndUtc)
            .AsNoTracking()
            .ToListAsync();

        var heldSlots = await _db.HeldSlots
            .Where(h => h.ProviderId == providerId &&
                        h.ExpiresAt  >  DateTime.UtcNow &&
                        h.StartUtc   >= dayStartUtc &&
                        h.StartUtc   <  dayEndUtc)
            .AsNoTracking()
            .ToListAsync();

        var breaks = await _db.Breaks
            .Where(b => b.ProviderId == providerId &&
                        b.StartUtc   <  dayEndUtc &&
                        b.EndUtc     >  dayStartUtc)
            .AsNoTracking()
            .ToListAsync();

        var slots = _availability.GetAvailableSlots(
            provider, ps.Service, provider.Policy,
            date, existingBookings, heldSlots, breaks);

        var slotDtos = slots.Select(s => new SlotDto(s.StartUtc, s.EndUtc, s.ProviderId, s.ServiceId, s.ProviderTimeZoneId)).ToList();

        return Ok(new AvailableSlotsResponseDto(date, providerId, serviceId, provider.TimeZoneId, slotDtos));
    }

    /// <summary>
    /// Place a short-lived hold on a slot so it is hidden from other customers
    /// while this customer completes the booking form.
    /// </summary>
    [HttpPost("hold")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(HoldSlotResponseDto), 201)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> HoldSlot([FromBody] HoldSlotDto dto)
    {
        // 5-minute hold window
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var token     = Guid.NewGuid().ToString("N");

        var ps = await _db.ProviderServices
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.ProviderId == dto.ProviderId && x.ServiceId == dto.ServiceId);

        if (ps is null)
            return NotFound("Provider / service combination not found.");

        var endUtc = dto.StartUtc.AddMinutes(ps.Service.DurationMinutes);

        // Check for an existing active hold or booking on this exact slot
        bool conflict = await _db.HeldSlots.AnyAsync(h =>
                            h.ProviderId == dto.ProviderId &&
                            h.StartUtc   == dto.StartUtc  &&
                            h.ExpiresAt  >  DateTime.UtcNow)
                        || await _db.Bookings.AnyAsync(b =>
                            b.ProviderId == dto.ProviderId &&
                            b.StartUtc   == dto.StartUtc  &&
                            b.Status     != Core.Enums.BookingStatus.Cancelled);

        if (conflict)
            return Conflict("Slot is already held or booked.");

        _db.HeldSlots.Add(new Core.Entities.HeldSlot
        {
            ProviderId   = dto.ProviderId,
            ServiceId    = dto.ServiceId,
            StartUtc     = dto.StartUtc,
            EndUtc       = endUtc,
            ExpiresAt    = expiresAt,
            SessionToken = token
        });

        await _db.SaveChangesAsync();
        return StatusCode(201, new HoldSlotResponseDto(token, expiresAt));
    }

    /// <summary>Release a hold the caller owns.</summary>
    [HttpDelete("hold/{sessionToken}")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ReleaseHold(string sessionToken)
    {
        var held = await _db.HeldSlots.FirstOrDefaultAsync(h => h.SessionToken == sessionToken);
        if (held is null) return NotFound();
        _db.HeldSlots.Remove(held);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static TimeZoneInfo GetTz(string id)
    {
        try   { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Utc; }
    }
}
