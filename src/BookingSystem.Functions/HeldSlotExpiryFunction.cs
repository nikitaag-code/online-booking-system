using BookingSystem.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookingSystem.Functions;

/// <summary>
/// Runs every minute. Removes held slots whose expiry has passed so they
/// become available again on the slot grid.
/// </summary>
public class HeldSlotExpiryFunction
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HeldSlotExpiryFunction> _log;

    public HeldSlotExpiryFunction(
        ApplicationDbContext db,
        ILogger<HeldSlotExpiryFunction> log)
    {
        _db  = db;
        _log = log;
    }

    [Function(nameof(HeldSlotExpiryFunction))]
    public async Task Run(
        [TimerTrigger("0 * * * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        var expired = await _db.HeldSlots
            .Where(h => h.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        _db.HeldSlots.RemoveRange(expired);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Expiry job: removed {Count} expired held slots.", expired.Count);
    }
}
