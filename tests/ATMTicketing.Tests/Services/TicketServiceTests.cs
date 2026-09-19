using ATMTicketing.Application.DTOs;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ATMTicketing.Infrastructure.Services;
using ATMTicketing.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Xunit;

namespace ATMTicketing.Tests.Services;

public class TicketServiceTests : IDisposable
{
    private readonly TestFixture _fixture = new();
    private readonly Mock<ATMTicketing.Application.Interfaces.Services.INotificationService> _notificationService = new();

    private TicketService CreateSut()
    {
        var slaService = new SlaService(_fixture.UnitOfWork, _notificationService.Object);
        var assignmentService = new AssignmentService(_fixture.UnitOfWork, _fixture.UserManager);
        return new TicketService(_fixture.UnitOfWork, slaService, assignmentService, _notificationService.Object, _fixture.CurrentUser, new InMemoryTicketNumberGenerator());
    }

    [Fact]
    public async Task CreateAsync_GeneratesTicketNumber_SetsSlaDueDates_AndAutoAssignsEngineer()
    {
        var (region, _, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var engineer = await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id, "Engineer One");
        _fixture.CurrentUser.UserId = agent.Id;

        var sut = CreateSut();
        var dto = new TicketCreateDto
        {
            AtmId = atm.Id,
            IncidentType = "Cash Jam",
            CategoryId = category.Id,
            Priority = PriorityLevel.Critical,
            Description = "Cash jammed in dispenser",
            ContactPerson = "Branch Manager",
            ContactNumber = "9999999999"
        };

        var result = await sut.CreateAsync(dto);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TicketNumber.Should().MatchRegex(@"^TCK-\d{6}-\d{6}$");
        result.Data.AssignedToName.Should().Be(engineer.FullName);
        result.Data.Status.Should().Be("Assigned");
        result.Data.ResponseDueAt.Should().NotBeNull();
        result.Data.ResolutionDueAt.Should().NotBeNull();
        (result.Data.ResolutionDueAt!.Value - result.Data.CreatedDate).Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromSeconds(5));
        result.Data.History.Should().Contain(h => h.ActionType == "Created");

        _notificationService.Verify(n => n.NotifyAsync(NotificationEvent.TicketCreated, It.IsAny<Ticket>(), agent.Id, default), Times.Once);
        _notificationService.Verify(n => n.NotifyAsync(NotificationEvent.TicketAssigned, It.IsAny<Ticket>(), engineer.Id, default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenAtmDoesNotExist()
    {
        var (region, _, _, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;

        var sut = CreateSut();
        var dto = new TicketCreateDto
        {
            AtmId = 9999,
            IncidentType = "Cash Jam",
            CategoryId = category.Id,
            Priority = PriorityLevel.Low,
            Description = "n/a",
            ContactPerson = "n/a",
            ContactNumber = "0000000000"
        };

        var result = await sut.CreateAsync(dto);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ATM"));
    }

    [Fact]
    public async Task CreateAsync_LeavesTicketNew_WhenNoEngineerIsAvailable()
    {
        var (region, _, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;

        var sut = CreateSut();
        var dto = new TicketCreateDto
        {
            AtmId = atm.Id,
            IncidentType = "Cash Jam",
            CategoryId = category.Id,
            Priority = PriorityLevel.Medium,
            Description = "n/a",
            ContactPerson = "n/a",
            ContactNumber = "0000000000"
        };

        var result = await sut.CreateAsync(dto);

        result.Succeeded.Should().BeTrue();
        result.Data!.AssignedToName.Should().BeNull();
        result.Data.Status.Should().Be("New");
    }

    [Fact]
    public async Task UpdateAsync_ChangingPriority_RecalculatesSlaDueDatesFromOriginalCreatedDate()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Low,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var ticketId = created.Data!.Id;
        var createdDate = created.Data.CreatedDate;

        var result = await sut.UpdateAsync(new TicketEditDto
        {
            Id = ticketId, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Critical,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });

        result.Succeeded.Should().BeTrue();
        var details = await sut.GetDetailsAsync(ticketId);
        details!.Priority.Should().Be("Critical");
        // Critical = 120 minutes resolution, measured from the ORIGINAL creation time, not from the edit.
        details.ResolutionDueAt.Should().BeCloseTo(createdDate.AddMinutes(120), TimeSpan.FromSeconds(5));
        details.History.Should().Contain(h => h.Notes != null && h.Notes.Contains("Priority: Low -> Critical"));
    }

    [Fact]
    public async Task UpdateAsync_Fails_OnClosedTicket()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var ticketId = created.Data!.Id;
        await sut.CloseAsync(new TicketCloseDto { TicketId = ticketId, ResolutionNotes = "done", ClosureRemarks = "done" });

        var result = await sut.UpdateAsync(new TicketEditDto
        {
            Id = ticketId, IncidentType = "Changed", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetForEditAsync_ReturnsCurrentFieldValues()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.High,
            Description = "Jammed", ContactPerson = "Branch Manager", ContactNumber = "9999999999"
        });

        var editDto = await sut.GetForEditAsync(created.Data!.Id);

        editDto.Should().NotBeNull();
        editDto!.IncidentType.Should().Be("Cash Jam");
        editDto.Priority.Should().Be(PriorityLevel.High);
        editDto.ContactPerson.Should().Be("Branch Manager");
    }

    [Fact]
    public async Task UpdateStatusAsync_TransitionsStatus_SetsRespondedAt_AndRecordsHistory()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var ticketId = created.Data!.Id;

        var inProgressStatusId = (int)TicketStatusCode.InProgress;
        var result = await sut.UpdateStatusAsync(new TicketStatusUpdateDto { TicketId = ticketId, NewStatusId = inProgressStatusId, Notes = "Started work" });

        result.Succeeded.Should().BeTrue();
        var details = await sut.GetDetailsAsync(ticketId);
        details!.Status.Should().Be("In Progress");
        details.RespondedAt.Should().NotBeNull();
        details.History.Should().Contain(h => h.ActionType == "StatusChanged" && h.Notes == "Started work");
    }

    [Fact]
    public async Task UpdateStatusAsync_ToResolved_SetsResolvedAt_AndNotifiesCreator()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var ticketId = created.Data!.Id;

        await sut.UpdateStatusAsync(new TicketStatusUpdateDto { TicketId = ticketId, NewStatusId = (int)TicketStatusCode.Resolved });

        var details = await sut.GetDetailsAsync(ticketId);
        details!.Status.Should().Be("Resolved");
        details.ResolvedAt.Should().NotBeNull();
        _notificationService.Verify(n => n.NotifyAsync(NotificationEvent.TicketResolved, It.IsAny<Ticket>(), agent.Id, default), Times.Once);
    }

    [Fact]
    public async Task AssignAsync_MarksPreviousAssignmentNotCurrent_WhenReassigning()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var engineerA = await _fixture.CreateUserAsync("a@test.local", Roles.FieldEngineer, region.Id, "Engineer A");
        var engineerB = await _fixture.CreateUserAsync("b@test.local", Roles.FieldEngineer, region.Id, "Engineer B");
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var ticketId = created.Data!.Id;
        var firstAssignee = created.Data.AssignedToId!;

        var otherEngineerId = firstAssignee == engineerA.Id ? engineerB.Id : engineerA.Id;
        var result = await sut.AssignAsync(new TicketAssignDto { TicketId = ticketId, EngineerId = otherEngineerId });

        result.Succeeded.Should().BeTrue();
        var assignments = _fixture.Db.TicketAssignments.Where(a => a.TicketId == ticketId).ToList();
        assignments.Should().HaveCount(2);
        assignments.Single(a => a.AssignedToId == firstAssignee).IsCurrent.Should().BeFalse();
        assignments.Single(a => a.AssignedToId == otherEngineerId).IsCurrent.Should().BeTrue();

        var details = await sut.GetDetailsAsync(ticketId);
        details!.AssignedToId.Should().Be(otherEngineerId);
    }

    [Fact]
    public async Task EscalateAsync_SetsEscalatedFlagAndStatus_AndNotifies()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var ticketId = created.Data!.Id;

        var result = await sut.EscalateAsync(ticketId, "Customer is a VIP branch");

        result.Succeeded.Should().BeTrue();
        var details = await sut.GetDetailsAsync(ticketId);
        details!.IsEscalated.Should().BeTrue();
        details.Status.Should().Be("Escalated");
        _notificationService.Verify(n => n.NotifyAsync(NotificationEvent.TicketEscalated, It.IsAny<Ticket>(), null, default), Times.Once);
    }

    [Fact]
    public async Task CloseAsync_SetsClosedAndResolutionFields_AndDefaultsResolvedAt()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto
        {
            AtmId = atm.Id, IncidentType = "Cash Jam", CategoryId = category.Id, Priority = PriorityLevel.Medium,
            Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000"
        });
        var ticketId = created.Data!.Id;

        var result = await sut.CloseAsync(new TicketCloseDto
        {
            TicketId = ticketId, ResolutionNotes = "Replaced dispenser part", ClosureRemarks = "Confirmed working with branch"
        });

        result.Succeeded.Should().BeTrue();
        var details = await sut.GetDetailsAsync(ticketId);
        details!.Status.Should().Be("Closed");
        details.ClosedAt.Should().NotBeNull();
        details.ResolvedAt.Should().NotBeNull(); // ResolvedAt defaults to ClosedAt when skipped straight to Close
        details.ResolutionNotes.Should().Be("Replaced dispenser part");
        details.ClosureRemarks.Should().Be("Confirmed working with branch");
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByPriority()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        await sut.CreateAsync(new TicketCreateDto { AtmId = atm.Id, IncidentType = "A", CategoryId = category.Id, Priority = PriorityLevel.Critical, Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000" });
        await sut.CreateAsync(new TicketCreateDto { AtmId = atm.Id, IncidentType = "B", CategoryId = category.Id, Priority = PriorityLevel.Low, Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000" });

        var page = await sut.GetPagedAsync(
            new DataTableRequest { Draw = 1, Start = 0, Length = 10 },
            new TicketFilterDto { Priority = "Critical" });

        page.RecordsTotal.Should().Be(2);
        page.RecordsFiltered.Should().Be(1);
        page.Data.Should().ContainSingle().Which.Priority.Should().Be("Critical");
    }

    [Fact]
    public async Task GetMyTicketsAsync_ReturnsOnlyOpenTicketsAssignedToThatEngineer()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);
        var engineer = await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id, "Engineer One");
        _fixture.CurrentUser.UserId = agent.Id;
        var sut = CreateSut();

        var created = await sut.CreateAsync(new TicketCreateDto { AtmId = atm.Id, IncidentType = "A", CategoryId = category.Id, Priority = PriorityLevel.High, Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000" });
        await sut.CloseAsync(new TicketCloseDto { TicketId = created.Data!.Id, ResolutionNotes = "done", ClosureRemarks = "done" });

        var openTicket = await sut.CreateAsync(new TicketCreateDto { AtmId = atm.Id, IncidentType = "B", CategoryId = category.Id, Priority = PriorityLevel.High, Description = "n/a", ContactPerson = "n/a", ContactNumber = "0000000000" });

        var myTickets = await sut.GetMyTicketsAsync(engineer.Id);

        myTickets.Should().ContainSingle(t => t.Id == openTicket.Data!.Id);
    }

    public void Dispose() => _fixture.Dispose();
}
