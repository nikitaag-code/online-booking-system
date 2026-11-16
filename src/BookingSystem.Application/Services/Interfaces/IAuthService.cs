using BookingSystem.Application.DTOs;

namespace BookingSystem.Application.Services.Interfaces;

public interface IAuthService
{
    Task<TokenResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<TokenResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<UserProfileDto> GetCurrentUserAsync(string userId, CancellationToken ct = default);
}
