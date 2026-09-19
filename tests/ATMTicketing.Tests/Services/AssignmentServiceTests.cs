using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ATMTicketing.Infrastructure.Services;
using ATMTicketing.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace ATMTicketing.Tests.Services;

public class AssignmentServiceTests : IDisposable
{
    private readonly TestFixture _fixture = new();

    private AssignmentService CreateSut() => new(_fixture.UnitOfWork, _fixture.UserManager);

    private async Task<Ticket> CreateOpenTicketAsync(Region region, Vendor vendor, Atm atm, CategoryMaster category, string createdById, string ticketNumber)
    {
        var ticket = new Ticket
        {
            TicketNumber = ticketNumber,
            AtmId = atm.Id,
            IncidentType = "ATM Down",
            CategoryId = category.Id,
            Priority = PriorityLevel.High,
            StatusId = (int)TicketStatusCode.New,
            Description = "Test",
            ContactPerson = "Contact",
            ContactNumber = "0000000000",
            CreatedById = createdById,
            RegionId = region.Id,
            AssignedVendorId = vendor.Id
        };
        _fixture.Db.Tickets.Add(ticket);
        await _fixture.Db.SaveChangesAsync();
        return ticket;
    }

    [Fact]
    public async Task FindBestEngineerAsync_PicksEngineerWithFewestOpenTickets_InSameRegion()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var busyEngineer = await _fixture.CreateUserAsync("busy@test.local", Roles.FieldEngineer, region.Id, "Busy Engineer");
        var idleEngineer = await _fixture.CreateUserAsync("idle@test.local", Roles.FieldEngineer, region.Id, "Idle Engineer");

        // Give the "busy" engineer two open tickets already assigned.
        for (var i = 0; i < 2; i++)
        {
            var t = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, $"TCK-BUSY-{i}");
            t.AssignedToId = busyEngineer.Id;
            t.StatusId = (int)TicketStatusCode.Assigned;
        }
        await _fixture.Db.SaveChangesAsync();

        var newTicket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-NEW-1");

        var sut = CreateSut();
        var best = await sut.FindBestEngineerAsync(newTicket);

        best.Should().NotBeNull();
        best!.Id.Should().Be(idleEngineer.Id);
    }

    [Fact]
    public async Task FindBestEngineerAsync_PrefersOnDutyEngineer_EvenOverAnIdleNonRosteredOne()
    {
        // The duty roster should be authoritative: once someone is rostered on for the
        // ticket's region today, they get the ticket even if a different, currently-idle
        // engineer in the same region would otherwise win purely on workload.
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var onDutyEngineer = await _fixture.CreateUserAsync("onduty@test.local", Roles.FieldEngineer, region.Id, "On Duty Engineer");
        var idleOffDutyEngineer = await _fixture.CreateUserAsync("idle@test.local", Roles.FieldEngineer, region.Id, "Idle Off-Duty Engineer");

        // Give the on-duty engineer an existing open ticket so they'd lose on workload alone.
        var existing = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-ONDUTY-0");
        existing.AssignedToId = onDutyEngineer.Id;
        existing.StatusId = (int)TicketStatusCode.Assigned;
        await _fixture.Db.SaveChangesAsync();

        _fixture.Db.DutyRosters.Add(new DutyRoster
        {
            DutyDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Shift = DutyShift.General,
            RegionId = region.Id,
            EngineerId = onDutyEngineer.Id
        });
        await _fixture.Db.SaveChangesAsync();

        var newTicket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-ONDUTY-1");

        var sut = CreateSut();
        var best = await sut.FindBestEngineerAsync(newTicket);

        best.Should().NotBeNull();
        best!.Id.Should().Be(onDutyEngineer.Id);
        idleOffDutyEngineer.Id.Should().NotBe(best.Id);
    }

    [Fact]
    public async Task FindBestEngineerAsync_FallsBackToRegionWorkloadRanking_WhenNobodyIsRosteredOnDuty()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var idleEngineer = await _fixture.CreateUserAsync("idle@test.local", Roles.FieldEngineer, region.Id, "Idle Engineer");

        // A duty roster entry for a *different* region must not affect this region's pick.
        var otherRegion = new Region { RegionName = "South", Zone = "Zone B" };
        _fixture.Db.Regions.Add(otherRegion);
        await _fixture.Db.SaveChangesAsync();
        var elsewhereEngineer = await _fixture.CreateUserAsync("elsewhere@test.local", Roles.FieldEngineer, otherRegion.Id, "Elsewhere Engineer");
        _fixture.Db.DutyRosters.Add(new DutyRoster
        {
            DutyDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Shift = DutyShift.General,
            RegionId = otherRegion.Id,
            EngineerId = elsewhereEngineer.Id
        });
        await _fixture.Db.SaveChangesAsync();

        var ticket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-NOROSTER-1");

        var sut = CreateSut();
        var best = await sut.FindBestEngineerAsync(ticket);

        best.Should().NotBeNull();
        best!.Id.Should().Be(idleEngineer.Id);
    }

    [Fact]
    public async Task FindBestEngineerAsync_FallsBackToAnyActiveEngineer_WhenNoneCoverTheRegion()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var otherRegion = new Region { RegionName = "South", Zone = "Zone B" };
        _fixture.Db.Regions.Add(otherRegion);
        await _fixture.Db.SaveChangesAsync();

        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var farEngineer = await _fixture.CreateUserAsync("far@test.local", Roles.FieldEngineer, otherRegion.Id, "Far Engineer");

        var ticket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-NEW-2");

        var sut = CreateSut();
        var best = await sut.FindBestEngineerAsync(ticket);

        best.Should().NotBeNull();
        best!.Id.Should().Be(farEngineer.Id);
    }

    [Fact]
    public async Task FindBestEngineerAsync_ReturnsNull_WhenNoEngineersExistAtAll()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var ticket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-NEW-3");

        var sut = CreateSut();
        var best = await sut.FindBestEngineerAsync(ticket);

        best.Should().BeNull();
    }

    [Fact]
    public async Task FindBestEngineerAsync_IgnoresInactiveEngineers()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var inactiveEngineer = await _fixture.CreateUserAsync("inactive@test.local", Roles.FieldEngineer, region.Id, "Inactive Engineer");
        inactiveEngineer.IsActive = false;
        await _fixture.UserManager.UpdateAsync(inactiveEngineer);

        var ticket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-NEW-4");

        var sut = CreateSut();
        var best = await sut.FindBestEngineerAsync(ticket);

        best.Should().BeNull();
    }

    [Fact]
    public async Task AutoAssignAsync_SetsAssignedToAndStatus_AndWritesHistoryAndAssignmentRows()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var engineer = await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id, "Engineer One");
        var ticket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-NEW-5");

        var sut = CreateSut();
        var assigned = await sut.AutoAssignAsync(ticket);
        await _fixture.UnitOfWork.SaveChangesAsync();

        assigned.Should().BeTrue();
        ticket.AssignedToId.Should().Be(engineer.Id);
        ticket.StatusId.Should().Be((int)TicketStatusCode.Assigned);

        var assignmentRows = _fixture.Db.TicketAssignments.Where(a => a.TicketId == ticket.Id).ToList();
        assignmentRows.Should().ContainSingle(a => a.IsCurrent && a.AssignedToId == engineer.Id);

        var historyRows = _fixture.Db.TicketHistories.Where(h => h.TicketId == ticket.Id).ToList();
        historyRows.Should().Contain(h => h.ActionType == TicketHistoryAction.Assigned);
    }

    [Fact]
    public async Task AutoAssignAsync_ReturnsFalse_AndLeavesTicketUnassigned_WhenNoEngineerAvailable()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var ticket = await CreateOpenTicketAsync(region, vendor, atm, category, agent.Id, "TCK-NEW-6");

        var sut = CreateSut();
        var assigned = await sut.AutoAssignAsync(ticket);

        assigned.Should().BeFalse();
        ticket.AssignedToId.Should().BeNull();
        ticket.StatusId.Should().Be((int)TicketStatusCode.New);
    }

    [Fact]
    public async Task GetAssignableEngineersAsync_IncludesEngineersWithZeroTickets()
    {
        // Regression test for the bug where the manual reassign dropdown only listed
        // engineers who already had at least one ticket assigned, so a brand-new engineer
        // (or a fresh system with no assignments yet) never appeared as an option.
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var freshEngineer = await _fixture.CreateUserAsync("fresh@test.local", Roles.FieldEngineer, region.Id, "Fresh Engineer");

        var sut = CreateSut();
        var options = await sut.GetAssignableEngineersAsync(region.Id);

        options.Should().ContainSingle(e => e.Id == freshEngineer.Id && e.OpenTicketCount == 0);
    }

    [Fact]
    public async Task GetAssignableEngineersAsync_ListsSameRegionEngineersFirst()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var otherRegion = new Region { RegionName = "South", Zone = "Zone B" };
        _fixture.Db.Regions.Add(otherRegion);
        await _fixture.Db.SaveChangesAsync();

        var sameRegionEngineer = await _fixture.CreateUserAsync("same@test.local", Roles.FieldEngineer, region.Id, "Same Region Engineer");
        var otherRegionEngineer = await _fixture.CreateUserAsync("other@test.local", Roles.FieldEngineer, otherRegion.Id, "Other Region Engineer");

        var sut = CreateSut();
        var options = await sut.GetAssignableEngineersAsync(region.Id);

        options.Should().HaveCount(2);
        options[0].Id.Should().Be(sameRegionEngineer.Id);
        options[0].IsSameRegionAsTicket.Should().BeTrue();
        options[1].Id.Should().Be(otherRegionEngineer.Id);
        options[1].IsSameRegionAsTicket.Should().BeFalse();
    }

    [Fact]
    public async Task GetAssignableEngineersAsync_ExcludesInactiveEngineers()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var inactive = await _fixture.CreateUserAsync("inactive@test.local", Roles.FieldEngineer, region.Id, "Inactive Engineer");
        inactive.IsActive = false;
        await _fixture.UserManager.UpdateAsync(inactive);

        var sut = CreateSut();
        var options = await sut.GetAssignableEngineersAsync(region.Id);

        options.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAssignableEngineersAsync_FlagsOnDutyEngineer_AndListsThemFirst()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var onDuty = await _fixture.CreateUserAsync("onduty@test.local", Roles.FieldEngineer, region.Id, "On Duty Engineer");
        var offDuty = await _fixture.CreateUserAsync("offduty@test.local", Roles.FieldEngineer, region.Id, "Off Duty Engineer");

        _fixture.Db.DutyRosters.Add(new DutyRoster
        {
            DutyDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Shift = DutyShift.General,
            RegionId = region.Id,
            EngineerId = onDuty.Id
        });
        await _fixture.Db.SaveChangesAsync();

        var sut = CreateSut();
        var options = await sut.GetAssignableEngineersAsync(region.Id);

        options.Should().HaveCount(2);
        options[0].Id.Should().Be(onDuty.Id);
        options[0].IsOnDuty.Should().BeTrue();
        options.Single(o => o.Id == offDuty.Id).IsOnDuty.Should().BeFalse();
    }

    public void Dispose() => _fixture.Dispose();
}
