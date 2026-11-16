namespace BookingSystem.Core.Entities;

public class Service
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }

    /// <summary>Buffer time added after the service ends before next slot can start.</summary>
    public int BufferMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProviderService> ProviderServices { get; set; } = new List<ProviderService>();
}
