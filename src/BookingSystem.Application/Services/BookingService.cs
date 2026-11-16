using BookingSystem.Application.DTOs;
using BookingSystem.Application.Exceptions;
using BookingSystem.Application.Services.Interfaces;
using BookingSystem.Core.Entities;
using BookingSystem.Core.Enums;
using BookingSystem.Core.Interfaces;
using BookingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Application.Services;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly IEmailService        _email;

    public BookingService(
        ApplicationDbContext db,
        IAvailabilityService availability,
        IEmailService        email)
    {
        _db           = db;
        _availability = availability;
        _email        = email;
    }

    public async Task<PagedResult<BookingDto>> GetMyBookingsAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Bookings
            .Where(b => b.CustomerId == customerId)
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .Include(b => b.Customer)
            .OrderByDescending(b => b.StartUtc)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<BookingDto>(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<BookingDto> GetBookingAsync(
        int id, string userId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        var b = await _db.Bookings
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .Include(b => b.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (b is null) throw new NotFoundException("Booking not found.");
        if (!CanAccess(b, userId, roles)) throw new ForbiddenException();
        return ToDto(b);
    }

    public async Task<BookingDto> CreateBookingAsync(
        CreateBookingDto dto, string customerId, CancellationToken ct = default)
    {
        if (dto.StartUtc <= DateTime.UtcNow)
            throw new ValidationException("Cannot book slots in the past.");

        var provider = await _db.Providers
            .Where(p => p.Id == dto.ProviderId && p.IsActive)
            .Include(p => p.WorkingHours)
            .Include(p => p.Policy)
            .FirstOrDefaultAsync(ct);

        if (provider is null) throw new NotFoundException("Provider not found.");

        var ps = await _db.ProviderServices
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.ProviderId == dto.ProviderId && x.ServiceId == dto.ServiceId, ct);

        if (ps is null) throw new NotFoundException("Service not offered by this provider.");

        var date             = DateOnly.FromDateTime(dto.StartUtc);
        var existingBookings = await _db.Bookings
            .Where(b => b.ProviderId == dto.ProviderId && b.StartUtc.Date == dto.StartUtc.Date)
            .AsNoTracking().ToListAsync(ct);

        var heldSlots = await _db.HeldSlots
            .Where(h => h.ProviderId     == dto.ProviderId &&
                        h.ExpiresAt      >  DateTime.UtcNow &&
                        h.StartUtc.Date  == dto.StartUtc.Date &&
                        h.SessionToken   != dto.SessionToken)
            .AsNoTracking().ToListAsync(ct);

        var breaks = await _db.Breaks
            .Where(b => b.ProviderId == dto.ProviderId && b.StartUtc.Date == dto.StartUtc.Date)
            .AsNoTracking().ToListAsync(ct);

        var availableSlots = _availability.GetAvailableSlots(
            provider, ps.Service, provider.Policy, date, existingBookings, heldSlots, breaks);

        if (!availableSlots.Any(s => s.StartUtc == dto.StartUtc))
            throw new ValidationException("The selected slot is not available.");

        var customer = await _db.Users.FindAsync(new object[] { customerId }, ct);

        var booking = new Booking
        {
            CustomerId = customerId,
            ProviderId = dto.ProviderId,
            ServiceId  = dto.ServiceId,
            StartUtc   = dto.StartUtc,
            EndUtc     = dto.StartUtc.AddMinutes(ps.Service.DurationMinutes),
            Status     = BookingStatus.Confirmed,
            Notes      = dto.Notes,
            CreatedAt  = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);

        if (!string.IsNullOrEmpty(dto.SessionToken))
        {
            var hold = await _db.HeldSlots
                .FirstOrDefaultAsync(h => h.SessionToken == dto.SessionToken, ct);
            if (hold is not null) _db.HeldSlots.Remove(hold);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            throw new ConflictException("This slot was just taken by another customer. Please choose a different time.");
        }

        _ = _email.SendBookingConfirmationAsync(
            customer?.Email ?? "", $"{customer?.FirstName} {customer?.LastName}",
            provider.Name, booking.StartUtc, customer?.TimeZoneId ?? "UTC", ps.Service.Name);

        await _db.Entry(booking).Reference(b => b.Provider).LoadAsync(ct);
        await _db.Entry(booking).Reference(b => b.Service).LoadAsync(ct);
        await _db.Entry(booking).Reference(b => b.Customer).LoadAsync(ct);

        return ToDto(booking);
    }

    public async Task<BookingDto> RescheduleAsync(
        int id, RescheduleBookingDto dto, string customerId, CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.Provider).ThenInclude(p => p.WorkingHours)
            .Include(b => b.Provider).ThenInclude(p => p.Policy)
            .Include(b => b.Service)
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (booking is null) throw new NotFoundException("Booking not found.");
        if (booking.CustomerId != customerId) throw new ForbiddenException();
        if (booking.Status != BookingStatus.Confirmed)
            throw new ValidationException("Only confirmed bookings can be rescheduled.");
        if (!CanCancelOrReschedule(booking))
            throw new ValidationException($"The cancellation window ({booking.Provider.Policy?.CancellationWindowHours ?? 0}h) for the original slot has passed.");
        if (dto.NewStartUtc <= DateTime.UtcNow)
            throw new ValidationException("New start time must be in the future.");

        var date         = DateOnly.FromDateTime(dto.NewStartUtc);
        var otherBookings = await _db.Bookings
            .Where(b => b.ProviderId == booking.ProviderId &&
                        b.Id         != booking.Id &&
                        b.StartUtc.Date == dto.NewStartUtc.Date)
            .AsNoTracking().ToListAsync(ct);

        var heldSlots = await _db.HeldSlots
            .Where(h => h.ProviderId    == booking.ProviderId &&
                        h.ExpiresAt     >  DateTime.UtcNow &&
                        h.StartUtc.Date == dto.NewStartUtc.Date)
            .AsNoTracking().ToListAsync(ct);

        var breaks = await _db.Breaks
            .Where(b => b.ProviderId == booking.ProviderId &&
                        b.StartUtc.Date == dto.NewStartUtc.Date)
            .AsNoTracking().ToListAsync(ct);

        var available = _availability.GetAvailableSlots(
            booking.Provider, booking.Service, booking.Provider.Policy,
            date, otherBookings, heldSlots, breaks);

        if (!available.Any(s => s.StartUtc == dto.NewStartUtc))
            throw new ValidationException("The requested new slot is not available.");

        booking.StartUtc  = dto.NewStartUtc;
        booking.EndUtc    = dto.NewStartUtc.AddMinutes(booking.Service.DurationMinutes);
        booking.Notes     = dto.Notes ?? booking.Notes;
        booking.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            throw new ConflictException("The new slot was just taken. Please choose a different time.");
        }

        return ToDto(booking);
    }

    public async Task CancelAsync(
        int id, string userId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.Provider).ThenInclude(p => p.Policy)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (booking is null) throw new NotFoundException("Booking not found.");
        if (!CanAccess(booking, userId, roles)) throw new ForbiddenException();
        if (booking.Status == BookingStatus.Cancelled)
            throw new ValidationException("Booking is already cancelled.");

        if (roles.Contains("Customer") && !CanCancelOrReschedule(booking))
            throw new ValidationException($"Cancellation window of {booking.Provider.Policy?.CancellationWindowHours ?? 0}h has closed.");

        booking.Status    = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool CanAccess(Booking b, string userId, IEnumerable<string> roles)
    {
        if (roles.Contains("Admin"))    return true;
        if (roles.Contains("Customer")) return b.CustomerId == userId;
        if (roles.Contains("Provider")) return b.Provider?.UserId == userId;
        return false;
    }

    private static bool CanCancelOrReschedule(Booking b)
    {
        if (b.Provider?.Policy is null) return true;
        return DateTime.UtcNow <= b.StartUtc.AddHours(-b.Provider.Policy.CancellationWindowHours);
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("unique",    StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("2601",      StringComparison.OrdinalIgnoreCase)
            || msg.Contains("2627",      StringComparison.OrdinalIgnoreCase);
    }

    private static BookingDto ToDto(Booking b) => new(
        b.Id,
        b.CustomerId,
        $"{b.Customer?.FirstName} {b.Customer?.LastName}".Trim(),
        b.ProviderId,
        b.Provider?.Name ?? "",
        b.ServiceId,
        b.Service?.Name  ?? "",
        b.StartUtc, b.EndUtc, b.Status,
        b.Notes, b.CreatedAt,
        b.Provider?.TimeZoneId ?? "UTC");
}
