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

    public void Dispose() => _fixture.Dispose();
}
