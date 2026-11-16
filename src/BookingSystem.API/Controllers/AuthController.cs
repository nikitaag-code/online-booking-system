using System.Security.Claims;
using BookingSystem.Application.DTOs;
using BookingSystem.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponseDto), 200)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _auth.RegisterAsync(dto, ct));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), 200)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _auth.LoginAsync(dto, ct));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    public async Task<IActionResult> Me(CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _auth.GetCurrentUserAsync(userId, ct));
    }
}
