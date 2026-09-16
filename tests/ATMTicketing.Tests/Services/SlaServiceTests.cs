using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ATMTicketing.Infrastructure.Services;
using ATMTicketing.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Xunit;

namespace ATMTicketing.Tests.Services;

public class SlaServiceTests : IDisposable
{
    private readonly TestFixture _fixture = new();
    private readonly Mock<INotificationService> _notificationService = new();

    private SlaService CreateSut() => new(_fixture.UnitOfWork, _notificationService.Object);

    [Theory]
    [InlineData(PriorityLevel.Critical, 15, 120)]
    [InlineData(PriorityLevel.High, 30, 240)]
    [InlineData(PriorityLevel.Medium, 60, 480)]
    [InlineData(PriorityLevel.Low, 240, 1440)]
    public async Task CalculateDueDates_UsesConfiguredMinutes_ForEachPriority(
        PriorityLevel priority, int expectedResponseMinutes, int expectedResolutionMinutes)
    {
        await _fixture.SeedBaselineAsync();
        var sut = CreateSut();
        var from = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var (responseDue, resolutionDue) = sut.CalculateDueDates(priority, from);

        responseDue.Should().Be(from.AddMinutes(expectedResponseMinutes));
        resolutionDue.Should().Be(from.AddMinutes(expectedResolutionMinutes));
    }

    [Fact]
    public async Task EvaluateOpenTicketsAsync_FlagsBreach_WhenResolutionDueDateHasPassed()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);

        var ticket = new Ticket
        {
            TicketNumber = "TCK-202601-000001",
            AtmId = atm.Id,
            IncidentType = "ATM Down",
            CategoryId = category.Id,
            Priority = PriorityLevel.Critical,
            StatusId = (int)TicketStatusCode.Assigned,
            Description = "Test",
            ContactPerson = "Contact",
            ContactNumber = "0000000000",
            CreatedById = agent.Id,
            RegionId = region.Id,
            AssignedVendorId = vendor.Id,
            ResolutionDueAt = DateTime.UtcNow.AddMinutes(-5) // already overdue
        };
        _fixture.Db.Tickets.Add(ticket);
        await _fixture.Db.SaveChangesAsync();

        var sut = CreateSut();
        var flagged = await sut.EvaluateOpenTicketsAsync();

        flagged.Should().Be(1);
        var reloaded = await _fixture.Db.Tickets.FindAsync(ticket.Id);
        reloaded!.IsResolutionBreached.Should().BeTrue();
        _notificationService.Verify(
            n => n.NotifyAsync(NotificationEvent.SlaBreached, It.Is<Ticket>(t => t.Id == ticket.Id), null, default),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateOpenTicketsAsync_DoesNotReFlagOrReNotify_TicketAlreadyBreached()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent2@test.local", Roles.CallCenterAgent, region.Id);

        var ticket = new Ticket
        {
            TicketNumber = "TCK-202601-000002",
            AtmId = atm.Id,
            IncidentType = "ATM Down",
            CategoryId = category.Id,
            Priority = PriorityLevel.Critical,
            StatusId = (int)TicketStatusCode.Assigned,
            Description = "Test",
            ContactPerson = "Contact",
            ContactNumber = "0000000000",
            CreatedById = agent.Id,
            RegionId = region.Id,
            AssignedVendorId = vendor.Id,
            ResolutionDueAt = DateTime.UtcNow.AddMinutes(-5),
            IsResolutionBreached = true // already flagged in a prior sweep
        };
        _fixture.Db.Tickets.Add(ticket);
        await _fixture.Db.SaveChangesAsync();

        var sut = CreateSut();
        var flagged = await sut.EvaluateOpenTicketsAsync();

        flagged.Should().Be(0);
        _notificationService.Verify(
            n => n.NotifyAsync(It.IsAny<NotificationEvent>(), It.IsAny<Ticket>(), It.IsAny<string?>(), default),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateOpenTicketsAsync_IgnoresResolvedTickets_EvenIfPastDue()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent3@test.local", Roles.CallCenterAgent, region.Id);

        var ticket = new Ticket
        {
            TicketNumber = "TCK-202601-000003",
            AtmId = atm.Id,
            IncidentType = "ATM Down",
            CategoryId = category.Id,
            Priority = PriorityLevel.Critical,
            StatusId = (int)TicketStatusCode.Resolved,
            Description = "Test",
            ContactPerson = "Contact",
            ContactNumber = "0000000000",
            CreatedById = agent.Id,
            RegionId = region.Id,
            AssignedVendorId = vendor.Id,
            ResolutionDueAt = DateTime.UtcNow.AddMinutes(-30),
            ResolvedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        _fixture.Db.Tickets.Add(ticket);
        await _fixture.Db.SaveChangesAsync();

        var sut = CreateSut();
        var flagged = await sut.EvaluateOpenTicketsAsync();

        flagged.Should().Be(0);
        var reloaded = await _fixture.Db.Tickets.FindAsync(ticket.Id);
        reloaded!.IsResolutionBreached.Should().BeFalse();
    }

    [Fact]
    public async Task GetConfigurationsAsync_ReturnsAllFourPriorities_OrderedByPriority()
    {
        await _fixture.SeedBaselineAsync();
        var sut = CreateSut();

        var configs = await sut.GetConfigurationsAsync();

        configs.Should().HaveCount(4);
        configs.Select(c => c.Priority).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task UpdateConfigurationAsync_PersistsNewThresholds()
    {
        await _fixture.SeedBaselineAsync();
        var sut = CreateSut();
        var critical = (await sut.GetConfigurationsAsync()).Single(c => c.Priority == PriorityLevel.Critical);
        critical.ResolutionMinutes = 90;

        var result = await sut.UpdateConfigurationAsync(critical);

        result.Succeeded.Should().BeTrue();
        var reloaded = await _fixture.Db.SlaConfigurations.FindAsync(critical.Id);
        reloaded!.ResolutionMinutes.Should().Be(90);
    }

    public void Dispose() => _fixture.Dispose();
}
