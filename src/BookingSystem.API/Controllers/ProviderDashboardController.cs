using BookingSystem.API.DTOs;
using BookingSystem.Core.Enums;
using BookingSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/provider/dashboard")]
[Authorize(Roles = "Provider")]
public class ProviderDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProviderDashboardController(ApplicationDbContext db) => _db = db;

    /// <summary>Returns all bookings for the provider's calendar.</summary>
    [HttpGet("bookings")]
    [ProducesResponseType(typeof(IEnumerable<BookingDto>), 200)]
    public async Task<IActionResult> GetCalendarBookings(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var provider = await GetCurrentProvider();
        if (provider is null) return NotFound("No provider profile found.");

        var bookings = await _db.Bookings
            .Where(b => b.ProviderId == provider.Id &&
                        b.StartUtc   >= from &&
                        b.StartUtc   <= to   &&
                        b.Status     != BookingStatus.Cancelled)
            .Include(b => b.Customer)
            .Include(b => b.Service)
            .Include(b => b.Provider)
            .AsNoTracking()
            .ToListAsync();

        return Ok(bookings.Select(b => new BookingDto(
            b.Id,
            b.CustomerId,
            $"{b.Customer?.FirstName} {b.Customer?.LastName}".Trim(),
            b.ProviderId,
            b.Provider?.Name ?? "",
            b.ServiceId,
            b.Service?.Name  ?? "",
            b.StartUtc, b.EndUtc, b.Status,
            b.Notes, b.CreatedAt,
            b.Provider?.TimeZoneId ?? "UTC")));
    }

    /// <summary>Returns utilization stats: bookings per day for a date range.</summary>
    [HttpGet("utilization")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetUtilization(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var provider = await GetCurrentProvider();
        if (provider is null) return NotFound();

        var bookings = await _db.Bookings
            .Where(b => b.ProviderId == provider.Id &&
                        b.StartUtc   >= from &&
                        b.StartUtc   <= to   &&
                        b.Status     != BookingStatus.Cancelled)
            .AsNoTracking()
            .ToListAsync();

        var byDay = bookings
            .GroupBy(b => b.StartUtc.Date)
            .Select(g => new
            {
                Date         = g.Key.ToString("yyyy-MM-dd"),
                BookingCount = g.Count(),
                TotalMinutes = g.Sum(b => (int)(b.EndUtc - b.StartUtc).TotalMinutes)
            })
            .OrderBy(x => x.Date)
            .ToList();

        var totalBookings = bookings.Count;
        var maxPerDay     = byDay.Count > 0 ? byDay.Max(d => d.BookingCount) : 0;
        var avgPerDay     = byDay.Count > 0 ? byDay.Average(d => d.BookingCount) : 0;

        return Ok(new
        {
            ProviderId    = provider.Id,
            ProviderName  = provider.Name,
            From          = from.ToString("yyyy-MM-dd"),
            To            = to.ToString("yyyy-MM-dd"),
            TotalBookings = totalBookings,
            MaxPerDay     = maxPerDay,
            AvgPerDay     = Math.Round(avgPerDay, 1),
            ByDay         = byDay
        });
    }

    private async Task<Core.Entities.Provider?> GetCurrentProvider()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await _db.Providers.FirstOrDefaultAsync(p => p.UserId == userId);
    }
}
