using BookingSystem.Application.DTOs;

namespace BookingSystem.Application.Services.Interfaces;

public interface IProviderDashboardService
{
    Task<IEnumerable<BookingDto>> GetCalendarBookingsAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<UtilizationDto> GetUtilizationAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default);
}
