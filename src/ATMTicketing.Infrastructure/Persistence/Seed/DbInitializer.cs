using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ATMTicketing.Infrastructure.Persistence.Seed;

/// <summary>Idempotent startup seeding: roles, admin user, master data, demo ATMs/vendors.</summary>
public static class DbInitializer
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger)
    {
        await context.Database.MigrateAsync();

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }

        if (!await context.Statuses.AnyAsync())
        {
            context.Statuses.AddRange(
                new StatusMaster { Id = (int)TicketStatusCode.New, Code = "NEW", DisplayName = "New", SortOrder = 1, ColorHex = "#0d6efd" },
                new StatusMaster { Id = (int)TicketStatusCode.Assigned, Code = "ASSIGNED", DisplayName = "Assigned", SortOrder = 2, ColorHex = "#6610f2" },
                new StatusMaster { Id = (int)TicketStatusCode.InProgress, Code = "IN_PROGRESS", DisplayName = "In Progress", SortOrder = 3, ColorHex = "#0dcaf0" },
                new StatusMaster { Id = (int)TicketStatusCode.PendingParts, Code = "PENDING_PARTS", DisplayName = "Pending Parts", SortOrder = 4, ColorHex = "#fd7e14" },
                new StatusMaster { Id = (int)TicketStatusCode.PendingCustomer, Code = "PENDING_CUSTOMER", DisplayName = "Pending Customer", SortOrder = 5, ColorHex = "#ffc107" },
                new StatusMaster { Id = (int)TicketStatusCode.Escalated, Code = "ESCALATED", DisplayName = "Escalated", SortOrder = 6, ColorHex = "#dc3545" },
                new StatusMaster { Id = (int)TicketStatusCode.Resolved, Code = "RESOLVED", DisplayName = "Resolved", SortOrder = 7, ColorHex = "#20c997", IsTerminal = false },
                new StatusMaster { Id = (int)TicketStatusCode.Closed, Code = "CLOSED", DisplayName = "Closed", SortOrder = 8, ColorHex = "#198754", IsTerminal = true },
                new StatusMaster { Id = (int)TicketStatusCode.Cancelled, Code = "CANCELLED", DisplayName = "Cancelled", SortOrder = 9, ColorHex = "#6c757d", IsTerminal = true }
            );
            await context.SaveChangesAsync();
        }

        if (!await context.Categories.AnyAsync())
        {
            var names = new[]
            {
                "ATM Down", "Cash Jam", "Cash Out", "Printer Failure", "Receipt Issue",
                "Network Failure", "Card Reader Issue", "Power Failure", "CCTV Failure",
                "Security Incident", "Preventive Maintenance"
            };
            context.Categories.AddRange(names.Select(n => new CategoryMaster { Name = n }));
            await context.SaveChangesAsync();
        }

        if (!await context.SlaConfigurations.AnyAsync())
        {
            context.SlaConfigurations.AddRange(
                new SlaConfiguration { Priority = PriorityLevel.Critical, ResponseMinutes = 15, ResolutionMinutes = 120, WarningThresholdPercent = 80 },
                new SlaConfiguration { Priority = PriorityLevel.High, ResponseMinutes = 30, ResolutionMinutes = 240, WarningThresholdPercent = 80 },
                new SlaConfiguration { Priority = PriorityLevel.Medium, ResponseMinutes = 60, ResolutionMinutes = 480, WarningThresholdPercent = 80 },
                new SlaConfiguration { Priority = PriorityLevel.Low, ResponseMinutes = 240, ResolutionMinutes = 1440, WarningThresholdPercent = 80 }
            );
            await context.SaveChangesAsync();
        }

        Region? north = null, south = null, east = null, west = null;
        if (!await context.Regions.AnyAsync())
        {
            north = new Region { RegionName = "North", Zone = "Zone A" };
            south = new Region { RegionName = "South", Zone = "Zone B" };
            east = new Region { RegionName = "East", Zone = "Zone C" };
            west = new Region { RegionName = "West", Zone = "Zone D" };
            context.Regions.AddRange(north, south, east, west);
            await context.SaveChangesAsync();
        }
        else
        {
            north = await context.Regions.FirstOrDefaultAsync(r => r.RegionName == "North");
            south = await context.Regions.FirstOrDefaultAsync(r => r.RegionName == "South");
            east = await context.Regions.FirstOrDefaultAsync(r => r.RegionName == "East");
            west = await context.Regions.FirstOrDefaultAsync(r => r.RegionName == "West");
        }

        Vendor? vendorA = null, vendorB = null;
        if (!await context.Vendors.AnyAsync() && north != null && south != null)
        {
            vendorA = new Vendor { VendorCode = "VEN-001", VendorName = "NCR Corporation", ContactPerson = "Rajesh Kumar", ContactNumber = "+91-9876543210", Email = "support@ncr-vendor.example", ServiceRegionId = north.Id };
            vendorB = new Vendor { VendorCode = "VEN-002", VendorName = "Diebold Nixdorf", ContactPerson = "Priya Sharma", ContactNumber = "+91-9876543211", Email = "support@diebold-vendor.example", ServiceRegionId = south.Id };
            context.Vendors.AddRange(vendorA, vendorB);
            await context.SaveChangesAsync();
        }
        else
        {
            vendorA = await context.Vendors.FirstOrDefaultAsync(v => v.VendorCode == "VEN-001");
            vendorB = await context.Vendors.FirstOrDefaultAsync(v => v.VendorCode == "VEN-002");
        }

        if (!await context.Atms.AnyAsync() && north != null && south != null && vendorA != null && vendorB != null)
        {
            context.Atms.AddRange(
                new Atm { AtmCode = "ATM-DEL-001", AtmName = "Connaught Place Branch ATM", BankName = "National Bank", RegionId = north.Id, Zone = north.Zone, State = "Delhi", City = "New Delhi", Address = "Connaught Place, New Delhi", VendorId = vendorA.Id, AtmType = AtmType.Onsite, Status = AtmOperationalStatus.Active, Latitude = 28.6315m, Longitude = 77.2167m },
                new Atm { AtmCode = "ATM-DEL-002", AtmName = "Karol Bagh ATM", BankName = "National Bank", RegionId = north.Id, Zone = north.Zone, State = "Delhi", City = "New Delhi", Address = "Karol Bagh, New Delhi", VendorId = vendorA.Id, AtmType = AtmType.Offsite, Status = AtmOperationalStatus.Active },
                new Atm { AtmCode = "ATM-BLR-001", AtmName = "MG Road Branch ATM", BankName = "National Bank", RegionId = south.Id, Zone = south.Zone, State = "Karnataka", City = "Bengaluru", Address = "MG Road, Bengaluru", VendorId = vendorB.Id, AtmType = AtmType.Onsite, Status = AtmOperationalStatus.Active },
                new Atm { AtmCode = "ATM-BLR-002", AtmName = "Whitefield ATM", BankName = "National Bank", RegionId = south.Id, Zone = south.Zone, State = "Karnataka", City = "Bengaluru", Address = "Whitefield, Bengaluru", VendorId = vendorB.Id, AtmType = AtmType.CashRecycler, Status = AtmOperationalStatus.UnderMaintenance }
            );
            await context.SaveChangesAsync();
        }

        if (await userManager.FindByEmailAsync("admin@atmticketing.local") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@atmticketing.local",
                Email = "admin@atmticketing.local",
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, Roles.Administrator);
            }
            else
            {
                logger.LogWarning("Failed to seed admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (north != null && await userManager.FindByEmailAsync("engineer1@atmticketing.local") is null)
        {
            var engineer = new ApplicationUser
            {
                UserName = "engineer1@atmticketing.local",
                Email = "engineer1@atmticketing.local",
                FullName = "Amit Verma",
                EmailConfirmed = true,
                IsActive = true,
                RegionId = north.Id
            };
            var result = await userManager.CreateAsync(engineer, "Engineer@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(engineer, Roles.FieldEngineer);
            }
        }
    }
}
