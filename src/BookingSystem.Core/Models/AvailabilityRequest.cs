namespace BookingSystem.Core.Models;

public record AvailabilityRequest(
    int ProviderId,
    int ServiceId,
    DateOnly Date);
