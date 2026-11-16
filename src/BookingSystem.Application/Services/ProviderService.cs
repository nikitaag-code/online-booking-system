using BookingSystem.Application.DTOs;
using BookingSystem.Application.Exceptions;
using BookingSystem.Application.Services.Interfaces;
using BookingSystem.Core.Entities;
using BookingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Application.Services;

public class ProviderService : IProviderService
{
    private readonly ApplicationDbContext _db;

    public ProviderService(ApplicationDbContext db) => _db = db;

    public async Task<IEnumerable<ProviderSummaryDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var providers = await _db.Providers
            .Where(p => p.IsActive)
            .Include(p => p.ProviderServices).ThenInclude(ps => ps.Service)
            .AsNoTracking()
            .ToListAsync(ct);

        return providers.Select(ToSummary);
    }

    public async Task<ProviderDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await _db.Providers
            .Where(p => p.Id == id && p.IsActive)
            .Include(p => p.WorkingHours)
            .Include(p => p.ProviderServices).ThenInclude(ps => ps.Service)
            .Include(p => p.Policy)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (p is null) throw new NotFoundException($"Provider {id} not found.");
        return ToDetail(p);
    }

    public async Task UpdateWorkingHoursAsync(string userId, UpdateWorkingHoursDto dto, CancellationToken ct = default)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (provider is null) throw new NotFoundException("No provider profile found for this account.");

        foreach (var input in dto.WorkingHours)
        {
            if (!TimeOnly.TryParse(input.StartTime, out var start) ||
                !TimeOnly.TryParse(input.EndTime,   out var end))
                throw new ValidationException($"Invalid time format for {input.DayOfWeek}.");

            if (start >= end)
                throw new ValidationException($"End time must be after start time for {input.DayOfWeek}.");
        }

        var existing = await _db.WorkingHours.Where(wh => wh.ProviderId == provider.Id).ToListAsync(ct);
        _db.WorkingHours.RemoveRange(existing);

        foreach (var input in dto.WorkingHours)
        {
            TimeOnly.TryParse(input.StartTime, out var start);
            TimeOnly.TryParse(input.EndTime,   out var end);
            _db.WorkingHours.Add(new WorkingHours
            {
                ProviderId = provider.Id,
                DayOfWeek  = input.DayOfWeek,
                StartTime  = start,
                EndTime    = end
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

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
