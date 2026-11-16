namespace BookingSystem.Core.Entities;

public class WorkingHours
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Start of working day in provider's local time (time-of-day only).</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>End of working day in provider's local time (time-of-day only).</summary>
    public TimeOnly EndTime { get; set; }

    public Provider Provider { get; set; } = null!;
}
