using BookingSystem.Application.DTOs;
using BookingSystem.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlotsController : ControllerBase
{
    private readonly ISlotService _slots;

    public SlotsController(ISlotService slots) => _slots = slots;

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(AvailableSlotsResponseDto), 200)]
    public async Task<IActionResult> GetSlots(
        [FromQuery] int providerId,
        [FromQuery] int serviceId,
        [FromQuery] DateOnly date,
        CancellationToken ct = default)
        => Ok(await _slots.GetAvailableSlotsAsync(providerId, serviceId, date, ct));

    [HttpPost("hold")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(HoldSlotResponseDto), 201)]
    public async Task<IActionResult> HoldSlot([FromBody] HoldSlotDto dto, CancellationToken ct = default)
    {
        var result = await _slots.HoldSlotAsync(dto, ct);
        return StatusCode(201, result);
    }

    [HttpDelete("hold/{sessionToken}")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ReleaseHold(string sessionToken, CancellationToken ct = default)
    {
        await _slots.ReleaseHoldAsync(sessionToken, ct);
        return NoContent();
    }
}
