using BookingSystem.API.DTOs;
using BookingSystem.Core.Entities;
using BookingSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvidersController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProvidersController(ApplicationDbContext db) => _db = db;

    /// <summary>List all active providers with their offered services.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProviderSummaryDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var providers = await _db.Providers
            .Where(p => p.IsActive)
            .Include(p => p.ProviderServices).ThenInclude(ps => ps.Service)
            .AsNoTracking()
            .ToListAsync();

        return Ok(providers.Select(ToSummary));
    }

    /// <summary>Get a single provider with working hours and policy.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProviderDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _db.Providers
            .Where(p => p.Id == id && p.IsActive)
            .Include(p => p.WorkingHours)
            .Include(p => p.ProviderServices).ThenInclude(ps => ps.Service)
            .Include(p => p.Policy)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return p is null ? NotFound() : Ok(ToDetail(p));
    }

    /// <summary>Update the authenticated provider's working hours.</summary>
    [HttpPut("me/working-hours")]
    [Authorize(Roles = "Provider")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> UpdateWorkingHours([FromBody] UpdateWorkingHoursDto dto)
    {
        var provider = await GetCurrentProvider();
        if (provider is null) return NotFound("No provider profile found for this account.");

        // Replace all working hours atomically
        var existing = await _db.WorkingHours.Where(wh => wh.ProviderId == provider.Id).ToListAsync();
        _db.WorkingHours.RemoveRange(existing);

        foreach (var input in dto.WorkingHours)
        {
            if (!TimeOnly.TryParse(input.StartTime, out var start) ||
                !TimeOnly.TryParse(input.EndTime,   out var end))
                return BadRequest($"Invalid time format for {input.DayOfWeek}.");

            if (start >= end)
                return BadRequest($"End time must be after start time for {input.DayOfWeek}.");

            _db.WorkingHours.Add(new WorkingHours
            {
                ProviderId = provider.Id,
                DayOfWeek  = input.DayOfWeek,
                StartTime  = start,
                EndTime    = end
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Provider?> GetCurrentProvider()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await _db.Providers.FirstOrDefaultAsync(p => p.UserId == userId);
    }

    private static ProviderSummaryDto ToSummary(Provider p) => new(
        p.Id, p.Name, p.Bio, p.TimeZoneId,
        p.ProviderServices.Select(ps => new ServiceDto(
            ps.Service.Id, ps.Service.Name, ps.Service.Description,
            ps.Service.DurationMinutes, ps.Service.BufferMinutes, ps.Price)));

    private static ProviderDetailDto ToDetail(Provider p) => new(
        p.Id, p.Name, p.Bio, p.TimeZoneId,
        p.WorkingHours.Select(wh => new WorkingHoursDto(
            wh.DayOfWeek, wh.StartTime.ToString("HH:mm"), wh.EndTime.ToString("HH:mm"))),
        p.ProviderServices.Select(ps => new ServiceDto(
            ps.Service.Id, ps.Service.Name, ps.Service.Description,
            ps.Service.DurationMinutes, ps.Service.BufferMinutes, ps.Price)),
        p.Policy is null ? null : new BookingPolicyDto(
            p.Policy.CancellationWindowHours, p.Policy.MaxBookingsPerDay));
}
