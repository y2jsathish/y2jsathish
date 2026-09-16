using ATMTicketing.Application.DTOs;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ATMTicketing.Infrastructure.Services;
using ATMTicketing.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Xunit;

namespace ATMTicketing.Tests.Services;

public class DashboardServiceTests : IDisposable
{
    private readonly TestFixture _fixture = new();
    private readonly Mock<ATMTicketing.Application.Interfaces.Services.INotificationService> _notificationService = new();

    private TicketService CreateTicketService()
    {
        var slaService = new SlaService(_fixture.UnitOfWork, _notificationService.Object);
        var assignmentService = new AssignmentService(_fixture.UnitOfWork, _fixture.UserManager);
        return new TicketService(_fixture.UnitOfWork, slaService, assignmentService, _notificationService.Object, _fixture.CurrentUser, new InMemoryTicketNumberGenerator());
    }

    [Fact]
    public async Task GetSummaryAsync_CountsOpenClosedAndBreachedCorrectly()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var ticketService = CreateTicketService();

        // One open critical ticket, one closed ticket.
        var open = await ticketService.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "A", CategoryId = category.Id, Priority = PriorityLevel.Critical,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var toClose = await ticketService.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "B", CategoryId = category.Id, Priority = PriorityLevel.Low,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        await ticketService.CloseAsync(new TicketCloseDto { TicketId = toClose.Data!.Id, ResolutionNotes = "done", ClosureRemarks = "done" });

        var dashboardService = new DashboardService(_fixture.UnitOfWork);
        var summary = await dashboardService.GetSummaryAsync();

        summary.TotalTickets.Should().Be(2);
        summary.OpenTickets.Should().Be(1);
        summary.ClosedTickets.Should().Be(1);
        summary.CriticalOpenTickets.Should().Be(1);
        open.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPriorityWiseTicketsAsync_OnlyCountsOpenTickets()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var ticketService = CreateTicketService();

        var toClose = await ticketService.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "A", CategoryId = category.Id, Priority = PriorityLevel.High,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        await ticketService.CloseAsync(new TicketCloseDto { TicketId = toClose.Data!.Id, ResolutionNotes = "done", ClosureRemarks = "done" });

        await ticketService.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "B", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });

        var dashboardService = new DashboardService(_fixture.UnitOfWork);
        var byPriority = await dashboardService.GetPriorityWiseTicketsAsync();

        byPriority.Should().ContainSingle(p => p.Name == "Medium" && p.Value == 1);
        byPriority.Should().NotContain(p => p.Name == "High");
    }

    public void Dispose() => _fixture.Dispose();
}
