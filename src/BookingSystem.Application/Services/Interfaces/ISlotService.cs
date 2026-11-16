using BookingSystem.Application.DTOs;

namespace BookingSystem.Application.Services.Interfaces;

public interface ISlotService
{
    Task<AvailableSlotsResponseDto> GetAvailableSlotsAsync(int providerId, int serviceId, DateOnly date, CancellationToken ct = default);
    Task<HoldSlotResponseDto> HoldSlotAsync(HoldSlotDto dto, CancellationToken ct = default);
    Task ReleaseHoldAsync(string sessionToken, CancellationToken ct = default);
}
