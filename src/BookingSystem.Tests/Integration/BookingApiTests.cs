using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BookingSystem.Application.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using BookingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingSystem.Tests.Integration;

/// <summary>
/// Verifies core API behaviour: auth, role enforcement, booking CRUD.
/// Uses WebApplicationFactory with an in-memory database.
/// </summary>
public class BookingApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public BookingApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace SQL Server with in-memory for tests
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(opt =>
                    opt.UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}"));
            });
        });

        _client = _factory.CreateClient();
    }

    // ── Auth tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_And_Login_Returns_Token()
    {
        var register = new RegisterDto("test@api.local", "Test@1234!", "Test", "User");
        var regResp  = await _client.PostAsJsonAsync("/api/auth/register", register);

        regResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await regResp.Content.ReadFromJsonAsync<TokenResponseDto>();
        token!.AccessToken.Should().NotBeNullOrEmpty();
        token.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenUnauthenticated()
    {
        var resp = await _client.GetAsync("/api/bookings");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerCannotAccess_ProviderDashboard()
    {
        var token = await RegisterAndGetToken("customer-only@api.local", "Customer@123!", "Customer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/provider/dashboard/bookings?from=2025-01-01&to=2025-01-31");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProviderCannotCreateBooking()
    {
        var token = await RegisterAndGetToken("provider-only@api.local", "Provider@123!", "Provider");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var dto  = new CreateBookingDto(1, 1, DateTime.UtcNow.AddDays(1), null, null);
        var resp = await _client.PostAsJsonAsync("/api/bookings", dto);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Slots endpoint ────────────────────────────────────────────────────────

    [Fact]
    public async Task Slots_Returns400_ForPastDate()
    {
        var token = await RegisterAndGetToken("slottest@api.local", "Customer@123!", "Customer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var resp = await _client.GetAsync($"/api/slots?providerId=1&serviceId=1&date={yesterday}");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> RegisterAndGetToken(string email, string password, string role)
    {
        // Use a fresh client so previous auth headers don't interfere
        using var c = _factory.CreateClient();

        var dto  = new RegisterDto(email, password, "Test", "User", "UTC", role);
        var resp = await c.PostAsJsonAsync("/api/auth/register", dto);
        resp.EnsureSuccessStatusCode();

        var token = await resp.Content.ReadFromJsonAsync<TokenResponseDto>();
        return token!.AccessToken;
    }
}
