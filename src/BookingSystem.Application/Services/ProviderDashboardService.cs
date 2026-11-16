using BookingSystem.Application.DTOs;
using BookingSystem.Application.Exceptions;
using BookingSystem.Application.Services.Interfaces;
using BookingSystem.Core.Enums;
using BookingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Application.Services;

public class ProviderDashboardService : IProviderDashboardService
{
    private readonly ApplicationDbContext _db;

    public ProviderDashboardService(ApplicationDbContext db) => _db = db;

    public async Task<IEnumerable<BookingDto>> GetCalendarBookingsAsync(
        string userId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (provider is null) throw new NotFoundException("No provider profile found.");

        var bookings = await _db.Bookings
            .Where(b => b.ProviderId == provider.Id &&
                        b.StartUtc   >= from &&
                        b.StartUtc   <= to   &&
                        b.Status     != BookingStatus.Cancelled)
            .Include(b => b.Customer)
            .Include(b => b.Service)
            .Include(b => b.Provider)
            .AsNoTracking()
            .ToListAsync(ct);

        return bookings.Select(b => new BookingDto(
            b.Id, b.CustomerId,
            $"{b.Customer?.FirstName} {b.Customer?.LastName}".Trim(),
            b.ProviderId, b.Provider?.Name ?? "",
            b.ServiceId,  b.Service?.Name  ?? "",
            b.StartUtc, b.EndUtc, b.Status,
            b.Notes, b.CreatedAt,
            b.Provider?.TimeZoneId ?? "UTC"));
    }

    public async Task<UtilizationDto> GetUtilizationAsync(
        string userId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (provider is null) throw new NotFoundException("No provider profile found.");

        var bookings = await _db.Bookings
            .Where(b => b.ProviderId == provider.Id &&
                        b.StartUtc   >= from &&
                        b.StartUtc   <= to   &&
                        b.Status     != BookingStatus.Cancelled)
            .AsNoTracking()
            .ToListAsync(ct);

        var byDay = bookings
            .GroupBy(b => b.StartUtc.Date)
            .Select(g => new DailyUtilizationDto(
                g.Key.ToString("yyyy-MM-dd"),
                g.Count(),
                g.Sum(b => (int)(b.EndUtc - b.StartUtc).TotalMinutes)))
            .OrderBy(x => x.Date)
            .ToList();

        return new UtilizationDto(
            ProviderId:    provider.Id,
            ProviderName:  provider.Name,
            From:          from.ToString("yyyy-MM-dd"),
            To:            to.ToString("yyyy-MM-dd"),
            TotalBookings: bookings.Count,
            MaxPerDay:     byDay.Count > 0 ? byDay.Max(d => d.BookingCount) : 0,
            AvgPerDay:     byDay.Count > 0 ? Math.Round(byDay.Average(d => d.BookingCount), 1) : 0,
            ByDay:         byDay);
    }
}
