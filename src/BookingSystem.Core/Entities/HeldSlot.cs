namespace BookingSystem.Core.Entities;

/// <summary>
/// A short-lived reservation placed when a customer begins the booking flow.
/// Prevents the slot from appearing available to others for a few minutes.
/// Expired holds are cleared by the Azure Function timer.
/// </summary>
public class HeldSlot
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public int ServiceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    /// <summary>When the hold expires if not converted to a confirmed booking.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Anonymous session token so the holder can release their own hold.</summary>
    public string SessionToken { get; set; } = string.Empty;

    public Provider Provider { get; set; } = null!;
}
