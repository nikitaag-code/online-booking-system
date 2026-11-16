namespace BookingSystem.Core.Entities;

public class Provider
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;

    /// <summary>IANA timezone identifier, e.g. "America/New_York"</summary>
    public string TimeZoneId { get; set; } = "UTC";
    public bool IsActive { get; set; } = true;

    public ApplicationUser User { get; set; } = null!;
    public ICollection<ProviderService> ProviderServices { get; set; } = new List<ProviderService>();
    public ICollection<WorkingHours> WorkingHours { get; set; } = new List<WorkingHours>();
    public ICollection<ProviderBreak> Breaks { get; set; } = new List<ProviderBreak>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public BookingPolicy? Policy { get; set; }
}
