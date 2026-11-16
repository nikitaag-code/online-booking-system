namespace BookingSystem.Core.Entities;

public class BookingPolicy
{
    public int Id { get; set; }
    public int ProviderId { get; set; }

    /// <summary>Hours before appointment when cancellation is no longer allowed.</summary>
    public int CancellationWindowHours { get; set; } = 24;

    /// <summary>Maximum number of bookings a provider accepts per calendar day.</summary>
    public int MaxBookingsPerDay { get; set; } = 20;

    public Provider Provider { get; set; } = null!;
}
