using System.ComponentModel.DataAnnotations;
using BookingSystem.Core.Enums;

namespace BookingSystem.API.DTOs;

public record CreateBookingDto(
    [Required] int ProviderId,
    [Required] int ServiceId,
    [Required] DateTime StartUtc,
    string? Notes,
    string? SessionToken);

public record RescheduleBookingDto(
    [Required] DateTime NewStartUtc,
    string? Notes);

public record BookingDto(
    int Id,
    string CustomerId,
    string CustomerName,
    int ProviderId,
    string ProviderName,
    int ServiceId,
    string ServiceName,
    DateTime StartUtc,
    DateTime EndUtc,
    BookingStatus Status,
    string? Notes,
    DateTime CreatedAt,
    string ProviderTimeZoneId);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
