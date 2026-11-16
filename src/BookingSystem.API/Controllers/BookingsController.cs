using BookingSystem.API.DTOs;
using BookingSystem.Core.Entities;
using BookingSystem.Core.Enums;
using BookingSystem.Core.Interfaces;
using BookingSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly IEmailService        _email;

    public BookingsController(
        ApplicationDbContext db,
        IAvailabilityService availability,
        IEmailService        email)
    {
        _db           = db;
        _availability = availability;
        _email        = email;
    }

    // ── Customer: list their own bookings ─────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(PagedResult<BookingDto>), 200)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var customerId = UserId();

        var query = _db.Bookings
            .Where(b => b.CustomerId == customerId)
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .Include(b => b.Customer)
            .OrderByDescending(b => b.StartUtc)
            .AsNoTracking();

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<BookingDto>(
            items.Select(ToDto).ToList(), total, page, pageSize));
    }

    // ── Get single booking (customer sees own; provider sees theirs; admin sees all) ──

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBooking(int id)
    {
        var b = await _db.Bookings
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .Include(b => b.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (b is null) return NotFound();

        if (!CanAccess(b)) return Forbid();

        return Ok(ToDto(b));
    }

    // ── Create booking ────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(BookingDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.StartUtc <= DateTime.UtcNow)
            return BadRequest("Cannot book slots in the past.");

        var provider = await _db.Providers
            .Where(p => p.Id == dto.ProviderId && p.IsActive)
            .Include(p => p.WorkingHours)
            .Include(p => p.Policy)
            .FirstOrDefaultAsync();

        if (provider is null) return NotFound("Provider not found.");

        var ps = await _db.ProviderServices
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.ProviderId == dto.ProviderId && x.ServiceId == dto.ServiceId);

        if (ps is null) return NotFound("Service not offered by this provider.");

        // Server-side availability re-validation (never trust the client).
        var date = DateOnly.FromDateTime(dto.StartUtc);

        var existingBookings = await _db.Bookings
            .Where(b => b.ProviderId == dto.ProviderId &&
                        b.StartUtc.Date == dto.StartUtc.Date)
            .AsNoTracking().ToListAsync();

        var heldSlots = await _db.HeldSlots
            .Where(h => h.ProviderId == dto.ProviderId &&
                        h.ExpiresAt  >  DateTime.UtcNow &&
                        h.StartUtc.Date == dto.StartUtc.Date &&
                        h.SessionToken  != dto.SessionToken)   // exclude caller's own hold
            .AsNoTracking().ToListAsync();

        var breaks = await _db.Breaks
            .Where(b => b.ProviderId == dto.ProviderId &&
                        b.StartUtc.Date == dto.StartUtc.Date)
            .AsNoTracking().ToListAsync();

        var availableSlots = _availability.GetAvailableSlots(
            provider, ps.Service, provider.Policy,
            date, existingBookings, heldSlots, breaks);

        bool valid = availableSlots.Any(s => s.StartUtc == dto.StartUtc);
        if (!valid)
            return BadRequest("The selected slot is not available.");

        var customerId = UserId();
        var customer   = await _db.Users.FindAsync(customerId);

        var booking = new Booking
        {
            CustomerId  = customerId!,
            ProviderId  = dto.ProviderId,
            ServiceId   = dto.ServiceId,
            StartUtc    = dto.StartUtc,
            EndUtc      = dto.StartUtc.AddMinutes(ps.Service.DurationMinutes),
            Status      = BookingStatus.Confirmed,
            Notes       = dto.Notes,
            CreatedAt   = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);

        // Release any held slot owned by this session
        if (!string.IsNullOrEmpty(dto.SessionToken))
        {
            var hold = await _db.HeldSlots.FirstOrDefaultAsync(h => h.SessionToken == dto.SessionToken);
            if (hold is not null) _db.HeldSlots.Remove(hold);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Another request beat us to it — the unique index fired.
            return Conflict("This slot was just taken by another customer. Please choose a different time.");
        }

        // Send confirmation email (stub — does not block the response)
        _ = _email.SendBookingConfirmationAsync(
            customer?.Email ?? "", $"{customer?.FirstName} {customer?.LastName}",
            provider.Name, booking.StartUtc, customer?.TimeZoneId ?? "UTC",
            ps.Service.Name);

        await _db.Entry(booking).Reference(b => b.Provider).LoadAsync();
        await _db.Entry(booking).Reference(b => b.Service).LoadAsync();
        await _db.Entry(booking).Reference(b => b.Customer).LoadAsync();

        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, ToDto(booking));
    }

    // ── Reschedule ────────────────────────────────────────────────────────────

    [HttpPut("{id:int}/reschedule")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleBookingDto dto)
    {
        var booking = await _db.Bookings
            .Include(b => b.Provider).ThenInclude(p => p.WorkingHours)
            .Include(b => b.Provider).ThenInclude(p => p.Policy)
            .Include(b => b.Service)
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();
        if (booking.CustomerId != UserId()) return Forbid();
        if (booking.Status != BookingStatus.Confirmed)
            return BadRequest("Only confirmed bookings can be rescheduled.");

        // Apply cancellation-window policy to the ORIGINAL slot
        if (!CanCancelOrReschedule(booking))
            return BadRequest($"The cancellation window ({booking.Provider.Policy?.CancellationWindowHours ?? 0}h) for the original slot has passed.");

        if (dto.NewStartUtc <= DateTime.UtcNow)
            return BadRequest("New start time must be in the future.");

        // Validate the new slot
        var date = DateOnly.FromDateTime(dto.NewStartUtc);

        var otherBookings = await _db.Bookings
            .Where(b => b.ProviderId == booking.ProviderId &&
                        b.Id         != booking.Id         &&
                        b.StartUtc.Date == dto.NewStartUtc.Date)
            .AsNoTracking().ToListAsync();

        var heldSlots = await _db.HeldSlots
            .Where(h => h.ProviderId == booking.ProviderId &&
                        h.ExpiresAt  >  DateTime.UtcNow   &&
                        h.StartUtc.Date == dto.NewStartUtc.Date)
            .AsNoTracking().ToListAsync();

        var breaks = await _db.Breaks
            .Where(b => b.ProviderId == booking.ProviderId &&
                        b.StartUtc.Date == dto.NewStartUtc.Date)
            .AsNoTracking().ToListAsync();

        var available = _availability.GetAvailableSlots(
            booking.Provider, booking.Service, booking.Provider.Policy,
            date, otherBookings, heldSlots, breaks);

        if (!available.Any(s => s.StartUtc == dto.NewStartUtc))
            return BadRequest("The requested new slot is not available.");

        booking.StartUtc  = dto.NewStartUtc;
        booking.EndUtc    = dto.NewStartUtc.AddMinutes(booking.Service.DurationMinutes);
        booking.Notes     = dto.Notes ?? booking.Notes;
        booking.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            return Conflict("The new slot was just taken. Please choose a different time.");
        }

        return Ok(ToDto(booking));
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Provider).ThenInclude(p => p.Policy)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();
        if (!CanAccess(booking)) return Forbid();

        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest("Booking is already cancelled.");

        // Customers must respect the cancellation window; Admins and Providers can always cancel.
        if (User.IsInRole("Customer") && !CanCancelOrReschedule(booking))
            return BadRequest($"Cancellation window of {booking.Provider.Policy?.CancellationWindowHours ?? 0}h has closed.");

        booking.Status    = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool CanAccess(Booking b)
    {
        if (User.IsInRole("Admin"))    return true;
        if (User.IsInRole("Customer")) return b.CustomerId == UserId();
        if (User.IsInRole("Provider"))
        {
            var userId = UserId();
            return b.Provider?.UserId == userId;
        }
        return false;
    }

    private static bool CanCancelOrReschedule(Booking b)
    {
        if (b.Provider?.Policy is null) return true;
        var deadline = b.StartUtc.AddHours(-b.Provider.Policy.CancellationWindowHours);
        return DateTime.UtcNow <= deadline;
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("2601", StringComparison.OrdinalIgnoreCase)   // SQL Server error code
            || msg.Contains("2627", StringComparison.OrdinalIgnoreCase);
    }

    private static BookingDto ToDto(Booking b) => new(
        b.Id,
        b.CustomerId,
        $"{b.Customer?.FirstName} {b.Customer?.LastName}".Trim(),
        b.ProviderId,
        b.Provider?.Name ?? "",
        b.ServiceId,
        b.Service?.Name  ?? "",
        b.StartUtc,
        b.EndUtc,
        b.Status,
        b.Notes,
        b.CreatedAt,
        b.Provider?.TimeZoneId ?? "UTC");
}
