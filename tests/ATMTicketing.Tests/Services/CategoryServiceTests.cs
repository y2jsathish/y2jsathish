using ATMTicketing.Application.DTOs;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ATMTicketing.Infrastructure.Services;
using ATMTicketing.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace ATMTicketing.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly TestFixture _fixture = new();

    private CategoryService CreateSut() => new(_fixture.UnitOfWork);

    [Fact]
    public async Task CreateAsync_AddsCategory_VisibleInGetAll()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(new CategoryEditDto { Name = "Skimming Device Found", Description = "Fraud device detected on ATM" });

        result.Succeeded.Should().BeTrue();
        var all = await sut.GetAllAsync();
        all.Should().ContainSingle(c => c.Name == "Skimming Device Found" && c.TicketCount == 0);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateName()
    {
        var sut = CreateSut();
        await sut.CreateAsync(new CategoryEditDto { Name = "Cash Jam" });

        var result = await sut.CreateAsync(new CategoryEditDto { Name = "Cash Jam" });

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_RenamesCategory()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(new CategoryEditDto { Name = "Old Name" });
        var category = (await sut.GetAllAsync()).Single();

        var result = await sut.UpdateAsync(new CategoryEditDto { Id = category.Id, Name = "New Name", IsActive = true });

        result.Succeeded.Should().BeTrue();
        (await sut.GetAllAsync()).Should().ContainSingle(c => c.Name == "New Name");
    }

    [Fact]
    public async Task DeleteAsync_RemovesUnusedCategory()
    {
        var sut = CreateSut();
        await sut.CreateAsync(new CategoryEditDto { Name = "Temporary" });
        var category = (await sut.GetAllAsync()).Single();

        var result = await sut.DeleteAsync(category.Id);

        result.Succeeded.Should().BeTrue();
        (await sut.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Fails_WhenCategoryHasTickets()
    {
        var (region, vendor, atm, category) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);

        _fixture.Db.Tickets.Add(new Ticket
        {
            TicketNumber = "TCK-1", AtmId = atm.Id, IncidentType = "ATM Down", CategoryId = category.Id,
            Priority = PriorityLevel.Low, StatusId = (int)TicketStatusCode.New, Description = "n/a",
            ContactPerson = "n/a", ContactNumber = "0000000000", CreatedById = agent.Id, RegionId = region.Id,
            AssignedVendorId = vendor.Id
        });
        await _fixture.Db.SaveChangesAsync();

        var sut = CreateSut();
        var result = await sut.DeleteAsync(category.Id);

        result.Succeeded.Should().BeFalse();
        (await sut.GetAllAsync()).Should().ContainSingle(c => c.Id == category.Id);
    }

    public void Dispose() => _fixture.Dispose();
}
