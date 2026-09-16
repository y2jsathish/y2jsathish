using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Infrastructure.Persistence;
using ATMTicketing.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ATMTicketing.Tests.EndToEnd;

/// <summary>
/// Boots the real ATMTicketing.Web app (real Program.cs, real routing, real Identity/RBAC,
/// real controllers and views) in-process, swapping only the SQL Server DbContext for an
/// isolated EF Core InMemory database per factory instance — everything else (auth cookies,
/// antiforgery, authorization policies, the SLA background service, DbInitializer seeding)
/// runs exactly as it would in production.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"E2E-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(DatabaseName));

            // The production ITicketNumberGenerator issues a raw SQL Server SEQUENCE query,
            // which the in-memory provider can't execute — swap in the same counter-based
            // double the unit tests use.
            var ticketNumberGenerator = services.SingleOrDefault(d => d.ServiceType == typeof(ITicketNumberGenerator));
            if (ticketNumberGenerator is not null)
            {
                services.Remove(ticketNumberGenerator);
            }
            services.AddSingleton<ITicketNumberGenerator, InMemoryTicketNumberGenerator>();
        });
    }

    public HttpClient CreateFlowClient(bool allowAutoRedirect = true) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            // TestServer never terminates real TLS, but scoping the client to an https://
            // base address makes it report HttpContext.Request.IsHttps = true, which the
            // app needs to accept the Identity cookie's CookieSecurePolicy.Always.
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = allowAutoRedirect
        });
}
