using BookingSystem.Core.Enums;

namespace BookingSystem.Core.Entities;

public class Booking
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public int ProviderId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>Appointment start in UTC. Combined with ProviderId forms the unique constraint.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Appointment end in UTC (StartUtc + service duration).</summary>
    public DateTime EndUtc { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool ReminderSent { get; set; }

    public ApplicationUser Customer { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
