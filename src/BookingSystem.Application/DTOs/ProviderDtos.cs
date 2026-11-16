namespace BookingSystem.Application.DTOs;

public record ProviderSummaryDto(
    int Id,
    string Name,
    string Bio,
    string TimeZoneId,
    IEnumerable<ServiceDto> Services);

public record ProviderDetailDto(
    int Id,
    string Name,
    string Bio,
    string TimeZoneId,
    IEnumerable<WorkingHoursDto> WorkingHours,
    IEnumerable<ServiceDto> Services,
    BookingPolicyDto? Policy);

public record WorkingHoursDto(
    DayOfWeek DayOfWeek,
    string StartTime,
    string EndTime);

public record BookingPolicyDto(
    int CancellationWindowHours,
    int MaxBookingsPerDay);

public record UpdateWorkingHoursDto(IEnumerable<WorkingHoursInputDto> WorkingHours);

public record WorkingHoursInputDto(
    DayOfWeek DayOfWeek,
    string StartTime,
    string EndTime);
