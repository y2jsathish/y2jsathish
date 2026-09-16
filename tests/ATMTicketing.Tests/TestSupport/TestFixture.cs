using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ATMTicketing.Infrastructure.Persistence;
using ATMTicketing.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ATMTicketing.Tests.TestSupport;

/// <summary>
/// Builds a fully wired (but isolated, EF Core InMemory-backed) slice of the app —
/// DbContext, UnitOfWork, and real ASP.NET Core Identity UserManager/RoleManager —
/// so service-layer tests exercise the same code paths as production without needing
/// a live SQL Server instance. Each instance gets its own uniquely named in-memory
/// database, so tests never see each other's data.
/// </summary>
public sealed class TestFixture : IDisposable
{
    private readonly ServiceProvider _provider;

    public ApplicationDbContext Db { get; }
    public UnitOfWork UnitOfWork { get; }
    public UserManager<ApplicationUser> UserManager { get; }
    public RoleManager<ApplicationRole> RoleManager { get; }
    public FakeCurrentUserService CurrentUser { get; } = new();

    public TestFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 4;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();

        Db = _provider.GetRequiredService<ApplicationDbContext>();
        UserManager = _provider.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager = _provider.GetRequiredService<RoleManager<ApplicationRole>>();
        UnitOfWork = new UnitOfWork(Db);
    }

    /// <summary>Seeds the same reference data DbInitializer seeds in production: statuses,
    /// categories, SLA config, a region, a vendor, and one active ATM — the minimum every
    /// ticket-workflow test needs to create a valid ticket.</summary>
    public async Task<(Region region, Vendor vendor, Atm atm, CategoryMaster category)> SeedBaselineAsync()
    {
        foreach (var status in new[]
                 {
                     new StatusMaster { Id = (int)TicketStatusCode.New, Code = "NEW", DisplayName = "New", SortOrder = 1, ColorHex = "#0d6efd" },
                     new StatusMaster { Id = (int)TicketStatusCode.Assigned, Code = "ASSIGNED", DisplayName = "Assigned", SortOrder = 2, ColorHex = "#6610f2" },
                     new StatusMaster { Id = (int)TicketStatusCode.InProgress, Code = "IN_PROGRESS", DisplayName = "In Progress", SortOrder = 3, ColorHex = "#0dcaf0" },
                     new StatusMaster { Id = (int)TicketStatusCode.PendingParts, Code = "PENDING_PARTS", DisplayName = "Pending Parts", SortOrder = 4, ColorHex = "#fd7e14" },
                     new StatusMaster { Id = (int)TicketStatusCode.PendingCustomer, Code = "PENDING_CUSTOMER", DisplayName = "Pending Customer", SortOrder = 5, ColorHex = "#ffc107" },
                     new StatusMaster { Id = (int)TicketStatusCode.Escalated, Code = "ESCALATED", DisplayName = "Escalated", SortOrder = 6, ColorHex = "#dc3545" },
                     new StatusMaster { Id = (int)TicketStatusCode.Resolved, Code = "RESOLVED", DisplayName = "Resolved", SortOrder = 7, ColorHex = "#20c997" },
                     new StatusMaster { Id = (int)TicketStatusCode.Closed, Code = "CLOSED", DisplayName = "Closed", SortOrder = 8, ColorHex = "#198754", IsTerminal = true },
                     new StatusMaster { Id = (int)TicketStatusCode.Cancelled, Code = "CANCELLED", DisplayName = "Cancelled", SortOrder = 9, ColorHex = "#6c757d", IsTerminal = true }
                 })
        {
            Db.Statuses.Add(status);
        }

        var category = new CategoryMaster { Name = "ATM Down" };
        Db.Categories.Add(category);

        Db.SlaConfigurations.AddRange(
            new SlaConfiguration { Priority = PriorityLevel.Critical, ResponseMinutes = 15, ResolutionMinutes = 120, WarningThresholdPercent = 80 },
            new SlaConfiguration { Priority = PriorityLevel.High, ResponseMinutes = 30, ResolutionMinutes = 240, WarningThresholdPercent = 80 },
            new SlaConfiguration { Priority = PriorityLevel.Medium, ResponseMinutes = 60, ResolutionMinutes = 480, WarningThresholdPercent = 80 },
            new SlaConfiguration { Priority = PriorityLevel.Low, ResponseMinutes = 240, ResolutionMinutes = 1440, WarningThresholdPercent = 80 });

        var region = new Region { RegionName = "North", Zone = "Zone A" };
        Db.Regions.Add(region);
        await Db.SaveChangesAsync();

        var vendor = new Vendor
        {
            VendorCode = "VEN-001", VendorName = "Test Vendor", ContactPerson = "Jane Doe",
            ContactNumber = "1234567890", Email = "vendor@test.local", ServiceRegionId = region.Id
        };
        Db.Vendors.Add(vendor);
        await Db.SaveChangesAsync();

        var atm = new Atm
        {
            AtmCode = "ATM-001", AtmName = "Test ATM", BankName = "Test Bank", RegionId = region.Id,
            Zone = region.Zone, State = "State", City = "City", Address = "Address",
            VendorId = vendor.Id, AtmType = AtmType.Onsite, Status = AtmOperationalStatus.Active
        };
        Db.Atms.Add(atm);
        await Db.SaveChangesAsync();

        return (region, vendor, atm, category);
    }

    public async Task<ApplicationUser> CreateUserAsync(string email, string role, int? regionId = null, string fullName = "Test User")
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName, IsActive = true,
            RegionId = regionId, EmailConfirmed = true
        };

        if (await RoleManager.FindByNameAsync(role) is null)
        {
            await RoleManager.CreateAsync(new ApplicationRole(role));
        }

        var result = await UserManager.CreateAsync(user, "Password1!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await UserManager.AddToRoleAsync(user, role);
        return user;
    }

    public void Dispose()
    {
        Db.Dispose();
        _provider.Dispose();
    }
}
