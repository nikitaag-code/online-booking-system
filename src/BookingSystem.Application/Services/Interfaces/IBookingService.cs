using BookingSystem.Application.DTOs;

namespace BookingSystem.Application.Services.Interfaces;

public interface IBookingService
{
    Task<PagedResult<BookingDto>> GetMyBookingsAsync(string customerId, int page, int pageSize, CancellationToken ct = default);
    Task<BookingDto> GetBookingAsync(int id, string userId, IEnumerable<string> roles, CancellationToken ct = default);
    Task<BookingDto> CreateBookingAsync(CreateBookingDto dto, string customerId, CancellationToken ct = default);
    Task<BookingDto> RescheduleAsync(int id, RescheduleBookingDto dto, string customerId, CancellationToken ct = default);
    Task CancelAsync(int id, string userId, IEnumerable<string> roles, CancellationToken ct = default);
}
