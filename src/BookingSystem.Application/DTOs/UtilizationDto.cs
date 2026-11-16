namespace BookingSystem.Application.DTOs;

public record DailyUtilizationDto(string Date, int BookingCount, int TotalMinutes);

public record UtilizationDto(
    int ProviderId,
    string ProviderName,
    string From,
    string To,
    int TotalBookings,
    int MaxPerDay,
    double AvgPerDay,
    IReadOnlyList<DailyUtilizationDto> ByDay);
