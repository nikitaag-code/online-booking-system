# BookingSystem — UC104 Online Appointment Booking Platform

A complete, full-stack booking platform built with **ASP.NET Core 8**, **EF Core 8**, **Angular 17**, and **Azure Functions**.

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 8.0+ |
| Node.js | 20+ |
| npm | 10+ |
| SQL Server or LocalDB | any recent |
| Azure Functions Core Tools | v4 (optional, for background jobs) |

---

## How to Run

### 1 · Backend API

```bash
cd src/BookingSystem.API

# (First time only) Apply EF Core migrations
dotnet ef database update

# Start the API
dotnet run
# → https://localhost:7001
# → Swagger: https://localhost:7001/swagger
```

The API seeds sample data automatically in development mode on first start:
- 3 providers (Dr. John Smith / Eastern, Lee's Hair Studio / Pacific, Maya Patel / GMT)
- 5 services with different durations and buffer times
- 1 sample booking within the cancellation window for demo purposes

### 2 · Angular Frontend

```bash
cd frontend/booking-app

npm install
npm start
# → http://localhost:4200
```

The Angular dev server proxies `/api/*` to `https://localhost:7001`.

### 3 · Azure Functions (optional — reminder + expiry jobs)

```bash
cd src/BookingSystem.Functions

# Install Azure Functions Core Tools if needed:
# npm install -g azure-functions-core-tools@4

func start
```

Update `local.settings.json` with your DB connection string.

### 4 · Run Tests

```bash
cd src/BookingSystem.Tests
dotnet test
```

---

## Demo Credentials

| Role | Email | Password |
|---|---|---|
| Customer | alice@example.local | Customer@123! |
| Customer | bob@example.local | Customer@123! |
| Provider | drsmith@booking.local | Provider@123! |
| Provider | hairbylee@booking.local | Provider@123! |
| Provider | consultant@booking.local | Provider@123! |
| Admin | admin@booking.local | Admin@123! |

---

## Architecture

```
BookingSystem.Core           ← Domain: entities, enums, pure AvailabilityService, interfaces
BookingSystem.Infrastructure ← EF Core DbContext, migrations, EmailService stub, SeedData
BookingSystem.API            ← ASP.NET Core 8 Web API (controllers, DTOs, JWT, rate limiting)
BookingSystem.Functions      ← Azure Functions: ReminderFunction, HeldSlotExpiryFunction
BookingSystem.Tests          ← xUnit: availability, policy, concurrent booking, API integration
frontend/booking-app         ← Angular 17 (standalone components, RxJS, Angular Material)
```

---

## Key Design Decisions

### Concurrency — no double bookings
The database is the final guard. A **unique index on `(ProviderId, StartUtc)`** in the `Bookings` table means that if two simultaneous requests target the same slot, the second `INSERT` raises a unique-constraint violation (SQL Server errors 2601/2627). The API catches `DbUpdateException`, inspects the message, and returns `409 Conflict`. Application-level "check-then-insert" is intentionally **not** used.

### Availability logic (pure, testable)
`AvailabilityService.GetAvailableSlots` accepts only in-memory data: provider, service, policy, existing bookings, held slots, and breaks. It has zero I/O and can be tested with simple object construction. The controller loads the required data and passes it in.

### Time zones
- All `DateTime` fields are stored in UTC (`datetime2` columns).
- Provider working hours are stored as `TimeOnly` (time-of-day, local to the provider's zone) together with a separate IANA `TimeZoneId` column.
- When generating slots, `TimeZoneInfo.ConvertTimeToUtc` is called on the **specific date** (not a generic offset), which correctly handles DST transitions (e.g., a 25-hour Sunday in autumn in New York does not break slot generation).
- The API returns all instants in UTC with the provider's `TimeZoneId` alongside, so the frontend can display both the customer's zone and the provider's zone.
- DST limitation: Windows timezone IDs are used (e.g., "Eastern Standard Time") because `TimeZoneInfo.FindSystemTimeZoneById` on Windows requires them. On Linux/Azure, IANA IDs are preferred; the `TimeZoneConverter` NuGet package can bridge the two. For now, seed data uses Windows IDs.

### Buffer time
Buffer minutes are part of the **Service**, not the Provider. When checking for slot conflicts, `slotEndWithBuffer = slotEnd + bufferDuration` is used, ensuring the slot immediately following a booked appointment is also blocked until the buffer expires.

### Held slots
Placing a slot hold (5 minutes) gives the customer time to fill in the booking form without the slot disappearing under them. The `HeldSlotExpiryFunction` cleans up expired holds every minute.

### Rate limiting
Built-in ASP.NET Core `RateLimiter` (fixed-window):
- **Booking endpoints**: 10 requests / minute / IP
- **Read endpoints**: 60 requests / minute / IP
Returns `429 Too Many Requests` on violation.

### Auth
- JWT bearer tokens with 8-hour expiry.
- Roles: `Customer`, `Provider`, `Admin` (enforced server-side via `[Authorize(Roles = "...")]`).
- OAuth/OpenID Connect is configured as a stub (see `Program.cs`). Set `Oidc:Authority`, `Oidc:ClientId`, `Oidc:ClientSecret` in environment/user-secrets to wire up a real OIDC provider (e.g., Auth0, Duende IdentityServer, Keycloak).
- **Never** store the JWT key or connection string in `appsettings.json` in production — use environment variables or Azure Key Vault.

### Frontend state
Angular services use `BehaviorSubject` streams. The slot grid polls every 30 seconds (via `interval`) and detects when a previously selected slot is no longer available, showing a friendly conflict message. The `UtcToLocalPipe` uses the browser's `Intl.DateTimeFormat` to convert any UTC ISO string to the user's timezone.

---

## API Summary

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | /api/auth/register | — | Register; returns JWT |
| POST | /api/auth/login | — | Login; returns JWT |
| GET  | /api/auth/me | Any | Current user profile |
| GET  | /api/providers | Any | List active providers |
| GET  | /api/providers/{id} | Any | Provider detail + working hours |
| PUT  | /api/providers/me/working-hours | Provider | Update working hours |
| GET  | /api/slots | Any | Available slots for provider+service+date |
| POST | /api/slots/hold | Customer | Place a 5-min hold on a slot |
| DELETE | /api/slots/hold/{token} | Customer | Release a hold |
| GET  | /api/bookings | Customer | Paginated list of own bookings |
| POST | /api/bookings | Customer | Create booking |
| GET  | /api/bookings/{id} | Customer/Provider/Admin | Booking detail |
| PUT  | /api/bookings/{id}/reschedule | Customer | Reschedule |
| DELETE | /api/bookings/{id} | Customer/Provider/Admin | Cancel |
| GET  | /api/provider/dashboard/bookings | Provider | Calendar bookings range |
| GET  | /api/provider/dashboard/utilization | Provider | Utilization stats |

---

## Known Limitations

1. **Windows vs IANA timezone IDs** — `TimeZoneInfo` on Windows uses Windows IDs; Linux/Docker deployments need IANA IDs or the `TimeZoneConverter` package.
2. **Recurring bookings / group bookings** — explicitly out of scope.
3. **Email** — the `EmailService` is a logging stub. Replace with SendGrid/SMTP in production.
4. **OAuth OIDC** — configured as a stub only; a real IdP requires additional settings.
5. **Concurrent booking test with SQLite** — the in-memory SQLite in `ConcurrentBookingTests` supports unique indexes but cannot replicate true SQL Server row-lock behavior. Run the same test against a real SQL Server for a full race-condition verification.
6. **EF Core migrations** — not committed (they are generated from the DbContext). Run `dotnet ef migrations add InitialCreate` from the `BookingSystem.API` directory to create them.

---

## Demo Walkthrough Checklist (10 min)

1. Register as a customer → provider list → pick Dr. Smith → General Consultation → slot grid
2. Select a slot → show it in customer's timezone AND provider's timezone (bottom of slot grid)
3. Confirm booking → My Bookings → Reschedule to a different day
4. Try to cancel the seed booking (within 24-hour window) → see policy error message
5. Open two browser tabs, both pick the same slot simultaneously → one gets 409
6. Run `func start` in `BookingSystem.Functions` → see reminder/expiry logs
7. Log in as a Provider → Calendar → weekly view → Utilization → bar chart
8. Trigger a 429 by calling the booking endpoint > 10 times / minute (use curl loop or Postman)
