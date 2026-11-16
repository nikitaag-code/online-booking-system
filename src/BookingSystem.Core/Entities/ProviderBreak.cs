namespace BookingSystem.Core.Entities;

public class ProviderBreak
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Break start in UTC.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Break end in UTC.</summary>
    public DateTime EndUtc { get; set; }

    public Provider Provider { get; set; } = null!;
}
