using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Infrastructure.BackgroundServices;
using ATMTicketing.Infrastructure.Persistence;
using ATMTicketing.Infrastructure.Persistence.Repositories;
using ATMTicketing.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ATMTicketing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IAtmService, AtmService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<ISlaService, SlaService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditService, AuditService>();

        services.AddHostedService<SlaMonitorService>();

        return services;
    }
}
