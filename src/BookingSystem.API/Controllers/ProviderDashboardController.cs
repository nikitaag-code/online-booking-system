using System.Security.Claims;
using BookingSystem.Application.DTOs;
using BookingSystem.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/provider/dashboard")]
[Authorize(Roles = "Provider")]
public class ProviderDashboardController : ControllerBase
{
    private readonly IProviderDashboardService _dashboard;

    public ProviderDashboardController(IProviderDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("bookings")]
    [ProducesResponseType(typeof(IEnumerable<BookingDto>), 200)]
    public async Task<IActionResult> GetCalendarBookings(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _dashboard.GetCalendarBookingsAsync(userId, from, to, ct));
    }

    [HttpGet("utilization")]
    [ProducesResponseType(typeof(UtilizationDto), 200)]
    public async Task<IActionResult> GetUtilization(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _dashboard.GetUtilizationAsync(userId, from, to, ct));
    }
}
