namespace BookingSystem.Core.Models;

public record SlotInfo(
    DateTime StartUtc,
    DateTime EndUtc,
    int ProviderId,
    int ServiceId,
    string ProviderTimeZoneId);
