using BookingSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BookingSystem.Infrastructure.Services;

/// <summary>
/// Stub email service — logs the message instead of sending real email.
/// Replace with a real SMTP/SendGrid provider in production.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger) => _logger = logger;

    public Task SendBookingConfirmationAsync(
        string toEmail, string customerName, string providerName,
        DateTime startUtc, string customerTimeZoneId, string serviceName,
        CancellationToken ct = default)
    {
        var localTime = ConvertToZone(startUtc, customerTimeZoneId);
        _logger.LogInformation(
            "[EMAIL STUB] Confirmation → {Email} | {Service} with {Provider} on {Time} ({Zone})",
            toEmail, serviceName, providerName, localTime, customerTimeZoneId);
        return Task.CompletedTask;
    }

    public Task SendReminderAsync(
        string toEmail, string customerName, string providerName,
        DateTime startUtc, string customerTimeZoneId, string serviceName,
        CancellationToken ct = default)
    {
        var localTime = ConvertToZone(startUtc, customerTimeZoneId);
        _logger.LogInformation(
            "[EMAIL STUB] Reminder   → {Email} | {Service} with {Provider} on {Time} ({Zone})",
            toEmail, serviceName, providerName, localTime, customerTimeZoneId);
        return Task.CompletedTask;
    }

    private static string ConvertToZone(DateTime utc, string tzId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            return local.ToString("ddd, MMM d yyyy h:mm tt zzz");
        }
        catch
        {
            return utc.ToString("u");
        }
    }
}
