namespace BookingSystem.Application.DTOs;

public record SlotDto(
    DateTime StartUtc,
    DateTime EndUtc,
    int ProviderId,
    int ServiceId,
    string ProviderTimeZoneId);

public record AvailableSlotsResponseDto(
    DateOnly Date,
    int ProviderId,
    int ServiceId,
    string ProviderTimeZoneId,
    IReadOnlyList<SlotDto> Slots);

public record HoldSlotDto(
    int ProviderId,
    int ServiceId,
    DateTime StartUtc);

public record HoldSlotResponseDto(
    string SessionToken,
    DateTime ExpiresAt);
