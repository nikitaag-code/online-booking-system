using System.Text;
using System.Threading.RateLimiting;
using BookingSystem.Application.Exceptions;
using BookingSystem.Application.Services;
using BookingSystem.Application.Services.Interfaces;
using BookingSystem.Core.Entities;
using BookingSystem.Core.Interfaces;
using BookingSystem.Core.Services;
using BookingSystem.Infrastructure.Data;
using BookingSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.Password.RequireDigit           = true;
        opt.Password.RequiredLength         = 8;
        opt.Password.RequireNonAlphanumeric = false;
        opt.User.RequireUniqueEmail         = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtKey    = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]   ?? "BookingSystem";
var jwtAud    = builder.Configuration["Jwt:Audience"] ?? "BookingSystem";

builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer          = true,
            ValidIssuer             = jwtIssuer,
            ValidateAudience        = true,
            ValidAudience           = jwtAud,
            ValidateLifetime        = true,
            ClockSkew               = TimeSpan.Zero
        };
    })
    .AddOpenIdConnect("oidc", opt =>
    {
        opt.Authority             = builder.Configuration["Oidc:Authority"] ?? "https://localhost:5001";
        opt.ClientId              = builder.Configuration["Oidc:ClientId"]  ?? "booking-client";
        opt.ClientSecret          = builder.Configuration["Oidc:ClientSecret"];
        opt.ResponseType          = "code";
        opt.SaveTokens            = true;
        opt.GetClaimsFromUserInfoEndpoint = true;
        opt.Scope.Add("openid");
        opt.Scope.Add("profile");
        opt.Scope.Add("email");
    });

builder.Services.AddAuthorization();

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("BookingPolicy", l =>
    {
        l.PermitLimit          = 10;
        l.Window               = TimeSpan.FromMinutes(1);
        l.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        l.QueueLimit           = 2;
    });

    opt.AddFixedWindowLimiter("ReadPolicy", l =>
    {
        l.PermitLimit          = 60;
        l.Window               = TimeSpan.FromMinutes(1);
        l.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        l.QueueLimit           = 5;
    });

    opt.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsync("Too many requests. Please retry shortly.");
    };
});

// ── CORS (dev) ────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt => opt.AddPolicy("Dev", p =>
    p.WithOrigins("http://localhost:4200")
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()));

// ── Domain / Infrastructure services ─────────────────────────────────────────
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IEmailService,        EmailService>();

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IBookingService,           BookingService>();
builder.Services.AddScoped<IProviderService,          BookingSystem.Application.Services.ProviderService>();
builder.Services.AddScoped<ISlotService,              SlotService>();
builder.Services.AddScoped<IAuthService,              AuthService>();
builder.Services.AddScoped<IProviderDashboardService, ProviderDashboardService>();

// ── MVC + Swagger ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Booking System API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type   = SecuritySchemeType.Http,
        Scheme = "bearer",
        In     = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Global exception handler (maps application exceptions to HTTP status codes)
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    ctx.Response.ContentType = "application/json";
    ctx.Response.StatusCode = ex switch
    {
        NotFoundException  => StatusCodes.Status404NotFound,
        ConflictException  => StatusCodes.Status409Conflict,
        ForbiddenException => StatusCodes.Status403Forbidden,
        ValidationException => StatusCodes.Status400BadRequest,
        _                  => StatusCodes.Status500InternalServerError
    };
    await ctx.Response.WriteAsJsonAsync(new { error = ex?.Message ?? "An unexpected error occurred." });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await BookingSystem.Infrastructure.Data.SeedData.SeedAsync(app.Services);
}

app.UseHttpsRedirection();
app.UseCors("Dev");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers()
   .RequireRateLimiting("ReadPolicy");

app.MapControllerRoute(
    name: "bookings-create",
    pattern: "api/bookings")
   .RequireRateLimiting("BookingPolicy");

app.Run();

public partial class Program { }
