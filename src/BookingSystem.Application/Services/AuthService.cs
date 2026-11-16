using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BookingSystem.Application.DTOs;
using BookingSystem.Application.Exceptions;
using BookingSystem.Application.Services.Interfaces;
using BookingSystem.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BookingSystem.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration              _config;

    public AuthService(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config      = config;
    }

    public async Task<TokenResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var allowedRoles = new[] { "Customer", "Provider" };
        if (!allowedRoles.Contains(dto.Role))
            throw new ValidationException("Role must be Customer or Provider.");

        var user = new ApplicationUser
        {
            UserName       = dto.Email,
            Email          = dto.Email,
            FirstName      = dto.FirstName,
            LastName       = dto.LastName,
            TimeZoneId     = dto.TimeZoneId,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, dto.Role);
        return await BuildTokenResponseAsync(user);
    }

    public async Task<TokenResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            throw new ValidationException("Invalid credentials.");

        return await BuildTokenResponseAsync(user);
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) throw new NotFoundException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        return new UserProfileDto(
            user.Id, user.Email, user.FirstName, user.LastName,
            user.TimeZoneId, roles.FirstOrDefault());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<TokenResponseDto> BuildTokenResponseAsync(ApplicationUser user)
    {
        var roles  = await _userManager.GetRolesAsync(user);
        var role   = roles.FirstOrDefault() ?? "Customer";
        var secret = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(8);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email,          user.Email!),
            new(ClaimTypes.Role,           role),
            new("timezone",               user.TimeZoneId)
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: creds);

        return new TokenResponseDto(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt:   expiry,
            UserId:      user.Id,
            Email:       user.Email!,
            Role:        role,
            TimeZoneId:  user.TimeZoneId);
    }
}
