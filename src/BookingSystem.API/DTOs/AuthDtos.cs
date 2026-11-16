using System.ComponentModel.DataAnnotations;

namespace BookingSystem.API.DTOs;

public record RegisterDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string FirstName,
    [Required] string LastName,
    string TimeZoneId = "UTC",
    string Role = "Customer");

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record TokenResponseDto(
    string AccessToken,
    DateTime ExpiresAt,
    string UserId,
    string Email,
    string Role,
    string TimeZoneId);
