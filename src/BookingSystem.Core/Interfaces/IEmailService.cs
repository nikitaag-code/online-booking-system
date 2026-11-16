namespace BookingSystem.Core.Interfaces;

public interface IEmailService
{
    Task SendBookingConfirmationAsync(string toEmail, string customerName, string providerName,
        DateTime startUtc, string customerTimeZoneId, string serviceName, CancellationToken ct = default);

    Task SendReminderAsync(string toEmail, string customerName, string providerName,
        DateTime startUtc, string customerTimeZoneId, string serviceName, CancellationToken ct = default);
}
