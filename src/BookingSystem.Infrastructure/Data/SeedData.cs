using BookingSystem.Core.Entities;
using BookingSystem.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingSystem.Infrastructure.Data;

/// <summary>Idempotent seed: safe to call on every startup in development.</summary>
public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        // ── Roles ──────────────────────────────────────────────────────────────
        foreach (var role in new[] { "Admin", "Provider", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Admin user ─────────────────────────────────────────────────────────
        await EnsureUser(userManager, "admin@booking.local", "Admin@123!", "Admin",
            "Administrator", "Super", "UTC");

        // ── Provider users ─────────────────────────────────────────────────────
        var p1User = await EnsureUser(userManager, "drsmith@booking.local", "Provider@123!", "Provider",
            "DrSmith", "John", "America/New_York");
        var p2User = await EnsureUser(userManager, "hairbylee@booking.local", "Provider@123!", "Provider",
            "HairByLee", "Lee", "America/Los_Angeles");
        var p3User = await EnsureUser(userManager, "consultant@booking.local", "Provider@123!", "Provider",
            "Consultant", "Maya", "Europe/London");

        // ── Customer users ─────────────────────────────────────────────────────
        await EnsureUser(userManager, "alice@example.local", "Customer@123!", "Customer",
            "Alice", "Alice", "America/Chicago");
        await EnsureUser(userManager, "bob@example.local", "Customer@123!", "Customer",
            "Bob", "Bob", "Asia/Tokyo");

        await db.SaveChangesAsync();

        // ── Services ───────────────────────────────────────────────────────────
        if (!await db.Services.AnyAsync())
        {
            db.Services.AddRange(
                new Service { Name = "General Consultation",  Description = "30-min GP visit",          DurationMinutes = 30, BufferMinutes = 10 },
                new Service { Name = "Follow-up",             Description = "15-min follow-up",          DurationMinutes = 15, BufferMinutes = 5  },
                new Service { Name = "Haircut",               Description = "Standard haircut",          DurationMinutes = 45, BufferMinutes = 15 },
                new Service { Name = "Hair Colour & Style",   Description = "Full colour treatment",     DurationMinutes = 90, BufferMinutes = 15 },
                new Service { Name = "Business Consulting",   Description = "60-min strategy session",   DurationMinutes = 60, BufferMinutes = 0  }
            );
            await db.SaveChangesAsync();
        }

        // ── Providers ──────────────────────────────────────────────────────────
        if (!await db.Providers.AnyAsync())
        {
            var allServices = await db.Services.ToListAsync();
            var svc = (Func<string, Service>)(name => allServices.First(s => s.Name == name));

            var dr = new Provider
            {
                UserId     = p1User.Id,
                Name       = "Dr. John Smith",
                Bio        = "Board-certified GP with 15 years' experience.",
                TimeZoneId = "Eastern Standard Time",   // Windows TZ ID
                IsActive   = true,
                WorkingHours = new List<WorkingHours>
                {
                    new() { DayOfWeek = DayOfWeek.Monday,    StartTime = new TimeOnly(9,0),  EndTime = new TimeOnly(17,0) },
                    new() { DayOfWeek = DayOfWeek.Tuesday,   StartTime = new TimeOnly(9,0),  EndTime = new TimeOnly(17,0) },
                    new() { DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(9,0),  EndTime = new TimeOnly(13,0) },
                    new() { DayOfWeek = DayOfWeek.Thursday,  StartTime = new TimeOnly(9,0),  EndTime = new TimeOnly(17,0) },
                    new() { DayOfWeek = DayOfWeek.Friday,    StartTime = new TimeOnly(9,0),  EndTime = new TimeOnly(16,0) },
                },
                Policy = new BookingPolicy { CancellationWindowHours = 24, MaxBookingsPerDay = 16 }
            };

            var lee = new Provider
            {
                UserId     = p2User.Id,
                Name       = "Lee's Hair Studio",
                Bio        = "Award-winning stylist in downtown LA.",
                TimeZoneId = "Pacific Standard Time",
                IsActive   = true,
                WorkingHours = new List<WorkingHours>
                {
                    new() { DayOfWeek = DayOfWeek.Tuesday,   StartTime = new TimeOnly(10,0), EndTime = new TimeOnly(19,0) },
                    new() { DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(10,0), EndTime = new TimeOnly(19,0) },
                    new() { DayOfWeek = DayOfWeek.Thursday,  StartTime = new TimeOnly(10,0), EndTime = new TimeOnly(19,0) },
                    new() { DayOfWeek = DayOfWeek.Friday,    StartTime = new TimeOnly(10,0), EndTime = new TimeOnly(19,0) },
                    new() { DayOfWeek = DayOfWeek.Saturday,  StartTime = new TimeOnly(9,0),  EndTime = new TimeOnly(17,0) },
                },
                Policy = new BookingPolicy { CancellationWindowHours = 48, MaxBookingsPerDay = 12 }
            };

            var maya = new Provider
            {
                UserId     = p3User.Id,
                Name       = "Maya Patel – Business Consultant",
                Bio        = "MBA, strategy consultant for startups and SMEs.",
                TimeZoneId = "GMT Standard Time",
                IsActive   = true,
                WorkingHours = new List<WorkingHours>
                {
                    new() { DayOfWeek = DayOfWeek.Monday,    StartTime = new TimeOnly(8,0),  EndTime = new TimeOnly(18,0) },
                    new() { DayOfWeek = DayOfWeek.Tuesday,   StartTime = new TimeOnly(8,0),  EndTime = new TimeOnly(18,0) },
                    new() { DayOfWeek = DayOfWeek.Thursday,  StartTime = new TimeOnly(8,0),  EndTime = new TimeOnly(18,0) },
                    new() { DayOfWeek = DayOfWeek.Friday,    StartTime = new TimeOnly(8,0),  EndTime = new TimeOnly(16,0) },
                },
                Policy = new BookingPolicy { CancellationWindowHours = 12, MaxBookingsPerDay = 8 }
            };

            db.Providers.AddRange(dr, lee, maya);
            await db.SaveChangesAsync();

            // Link services to providers
            db.ProviderServices.AddRange(
                new ProviderService { ProviderId = dr.Id,   ServiceId = svc("General Consultation").Id, Price = 150m },
                new ProviderService { ProviderId = dr.Id,   ServiceId = svc("Follow-up").Id,            Price = 75m  },
                new ProviderService { ProviderId = lee.Id,  ServiceId = svc("Haircut").Id,              Price = 60m  },
                new ProviderService { ProviderId = lee.Id,  ServiceId = svc("Hair Colour & Style").Id,  Price = 150m },
                new ProviderService { ProviderId = maya.Id, ServiceId = svc("Business Consulting").Id,  Price = 300m }
            );
            await db.SaveChangesAsync();

            // Add a sample booking that is close to the cancellation window (< 24h away)
            var aliceUser = await userManager.FindByEmailAsync("alice@example.local");
            if (aliceUser is not null)
            {
                db.Bookings.Add(new Booking
                {
                    CustomerId  = aliceUser.Id,
                    ProviderId  = dr.Id,
                    ServiceId   = svc("Follow-up").Id,
                    StartUtc    = DateTime.UtcNow.AddHours(12),
                    EndUtc      = DateTime.UtcNow.AddHours(12).AddMinutes(15),
                    Status      = BookingStatus.Confirmed,
                    Notes       = "Seed booking within cancellation window – used for demo.",
                    CreatedAt   = DateTime.UtcNow.AddDays(-3)
                });
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUser(
        UserManager<ApplicationUser> um,
        string email, string password, string role,
        string userName, string firstName, string timeZone)
    {
        var user = await um.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName   = userName,
                Email      = email,
                FirstName  = firstName,
                LastName   = role,
                TimeZoneId = timeZone,
                EmailConfirmed = true
            };
            var result = await um.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        if (!await um.IsInRoleAsync(user, role))
            await um.AddToRoleAsync(user, role);
        return user;
    }
}
