using Microsoft.AspNetCore.Identity;

namespace BookingSystem.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Provider? Provider { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
