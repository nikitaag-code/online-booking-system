using BookingSystem.Core.Interfaces;
using BookingSystem.Infrastructure.Data;
using BookingSystem.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        services.AddDbContext<ApplicationDbContext>(opt =>
            opt.UseSqlServer(ctx.Configuration["SqlConnectionString"]));

        services.AddLogging();
        services.AddScoped<IEmailService, EmailService>();
    })
    .Build();

host.Run();
