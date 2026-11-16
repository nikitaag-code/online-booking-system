using BookingSystem.Core.Enums;
using BookingSystem.Core.Interfaces;
using BookingSystem.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookingSystem.Functions;

/// <summary>
/// Runs every 15 minutes. Sends reminder emails for bookings starting in the next
/// 24–25 hours that have not yet been reminded.
/// </summary>
public class ReminderFunction
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService        _email;
    private readonly ILogger<ReminderFunction> _log;

    public ReminderFunction(
        ApplicationDbContext db,
        IEmailService email,
        ILogger<ReminderFunction> log)
    {
        _db    = db;
        _email = email;
        _log   = log;
    }

    [Function(nameof(ReminderFunction))]
    public async Task Run(
        [TimerTrigger("0 */15 * * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        var from = DateTime.UtcNow.AddHours(24);
        var to   = DateTime.UtcNow.AddHours(25);

        var upcoming = await _db.Bookings
            .Where(b => b.Status       == BookingStatus.Confirmed &&
                        !b.ReminderSent &&
                        b.StartUtc     >= from &&
                        b.StartUtc     <  to)
            .Include(b => b.Customer)
            .Include(b => b.Provider)
            .Include(b => b.Service)
            .ToListAsync(ct);

        _log.LogInformation("Reminder job: {Count} bookings to remind.", upcoming.Count);

        foreach (var booking in upcoming)
        {
            try
            {
                await _email.SendReminderAsync(
                    booking.Customer?.Email    ?? "",
                    $"{booking.Customer?.FirstName} {booking.Customer?.LastName}".Trim(),
                    booking.Provider?.Name     ?? "",
                    booking.StartUtc,
                    booking.Customer?.TimeZoneId ?? "UTC",
                    booking.Service?.Name       ?? "",
                    ct);

                booking.ReminderSent = true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send reminder for booking {Id}.", booking.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
