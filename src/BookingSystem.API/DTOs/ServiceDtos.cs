namespace BookingSystem.API.DTOs;

public record ServiceDto(
    int Id,
    string Name,
    string Description,
    int DurationMinutes,
    int BufferMinutes,
    decimal? Price);
