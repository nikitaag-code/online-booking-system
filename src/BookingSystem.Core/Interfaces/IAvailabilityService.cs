using BookingSystem.Core.Entities;
using BookingSystem.Core.Models;

namespace BookingSystem.Core.Interfaces;

public interface IAvailabilityService
{
    IReadOnlyList<SlotInfo> GetAvailableSlots(
        Provider provider,
        Service service,
        BookingPolicy? policy,
        DateOnly date,
        IReadOnlyList<Booking> existingBookings,
        IReadOnlyList<HeldSlot> heldSlots,
        IReadOnlyList<ProviderBreak> breaks);
}
