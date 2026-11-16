using BookingSystem.Application.DTOs;

namespace BookingSystem.Application.Services.Interfaces;

public interface IProviderService
{
    Task<IEnumerable<ProviderSummaryDto>> GetAllActiveAsync(CancellationToken ct = default);
    Task<ProviderDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task UpdateWorkingHoursAsync(string userId, UpdateWorkingHoursDto dto, CancellationToken ct = default);
}
