using System.Security.Claims;
using BookingSystem.Application.DTOs;
using BookingSystem.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings) => _bookings = bookings;

    [HttpGet]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(PagedResult<BookingDto>), 200)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _bookings.GetMyBookingsAsync(UserId()!, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookingDto), 200)]
    public async Task<IActionResult> GetBooking(int id, CancellationToken ct = default)
        => Ok(await _bookings.GetBookingAsync(id, UserId()!, Roles(), ct));

    [HttpPost]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(BookingDto), 201)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var booking = await _bookings.CreateBookingAsync(dto, UserId()!, ct);
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
    }

    [HttpPut("{id:int}/reschedule")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(BookingDto), 200)]
    public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleBookingDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _bookings.RescheduleAsync(id, dto, UserId()!, ct));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct = default)
    {
        await _bookings.CancelAsync(id, UserId()!, Roles(), ct);
        return NoContent();
    }

    private string? UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private IEnumerable<string> Roles() =>
        User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);
}
