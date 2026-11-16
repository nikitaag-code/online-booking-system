using System.Security.Claims;
using BookingSystem.Application.DTOs;
using BookingSystem.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvidersController : ControllerBase
{
    private readonly IProviderService _providers;

    public ProvidersController(IProviderService providers) => _providers = providers;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProviderSummaryDto>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
        => Ok(await _providers.GetAllActiveAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProviderDetailDto), 200)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
        => Ok(await _providers.GetByIdAsync(id, ct));

    [HttpPut("me/working-hours")]
    [Authorize(Roles = "Provider")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> UpdateWorkingHours([FromBody] UpdateWorkingHoursDto dto, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _providers.UpdateWorkingHoursAsync(userId, dto, ct);
        return NoContent();
    }
}
