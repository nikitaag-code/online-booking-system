namespace BookingSystem.Core.Entities;

public class ProviderService
{
    public int ProviderId { get; set; }
    public int ServiceId { get; set; }
    public decimal Price { get; set; }

    public Provider Provider { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
