using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BookingSystem.API.DTOs;
using BookingSystem.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config      = config;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var allowedRoles = new[] { "Customer", "Provider" };
        if (!allowedRoles.Contains(dto.Role))
            return BadRequest("Role must be Customer or Provider.");

        var user = new ApplicationUser
        {
            UserName      = dto.Email,
            Email         = dto.Email,
            FirstName     = dto.FirstName,
            LastName      = dto.LastName,
            TimeZoneId    = dto.TimeZoneId,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, dto.Role);
        return Ok(await BuildTokenResponse(user));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized("Invalid credentials.");

        return Ok(await BuildTokenResponse(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user   = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.TimeZoneId,
            Role = roles.FirstOrDefault()
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<TokenResponseDto> BuildTokenResponse(ApplicationUser user)
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
