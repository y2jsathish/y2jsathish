using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ATMTicketing.Infrastructure.Services;
using ATMTicketing.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace ATMTicketing.Tests.Services;

public class DutyRosterServiceTests : IDisposable
{
    private readonly TestFixture _fixture = new();

    private DutyRosterService CreateSut() => new(_fixture.UnitOfWork, _fixture.UserManager);

    [Fact]
    public async Task CreateAsync_AddsRosterEntry_VisibleInGetAsync()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var engineer = await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id, "Engineer One");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sut = CreateSut();
        var result = await sut.CreateAsync(new()
        {
            DutyDate = today,
            Shift = DutyShift.Morning,
            RegionId = region.Id,
            EngineerId = engineer.Id
        });

        result.Succeeded.Should().BeTrue();
        var rows = await sut.GetAsync(today, today, region.Id);
        rows.Should().ContainSingle(r => r.EngineerId == engineer.Id && r.Shift == DutyShift.Morning);
    }

    [Fact]
    public async Task CreateAsync_RejectsNonEngineerUser()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var agent = await _fixture.CreateUserAsync("agent@test.local", Roles.CallCenterAgent, region.Id);

        var sut = CreateSut();
        var result = await sut.CreateAsync(new()
        {
            DutyDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Shift = DutyShift.General,
            RegionId = region.Id,
            EngineerId = agent.Id
        });

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateEngineerDateShift()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var engineer = await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id, "Engineer One");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sut = CreateSut();
        await sut.CreateAsync(new() { DutyDate = today, Shift = DutyShift.Morning, RegionId = region.Id, EngineerId = engineer.Id });
        var result = await sut.CreateAsync(new() { DutyDate = today, Shift = DutyShift.Morning, RegionId = region.Id, EngineerId = engineer.Id });

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ChangesShift()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var engineer = await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id, "Engineer One");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sut = CreateSut();
        await sut.CreateAsync(new() { DutyDate = today, Shift = DutyShift.Morning, RegionId = region.Id, EngineerId = engineer.Id });
        var created = (await sut.GetAsync(today, today, region.Id)).Single();

        var result = await sut.UpdateAsync(new()
        {
            Id = created.Id,
            DutyDate = today,
            Shift = DutyShift.Night,
            RegionId = region.Id,
            EngineerId = engineer.Id
        });

        result.Succeeded.Should().BeTrue();
        var updated = (await sut.GetAsync(today, today, region.Id)).Single();
        updated.Shift.Should().Be(DutyShift.Night);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        var engineer = await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id, "Engineer One");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sut = CreateSut();
        await sut.CreateAsync(new() { DutyDate = today, Shift = DutyShift.General, RegionId = region.Id, EngineerId = engineer.Id });
        var created = (await sut.GetAsync(today, today, region.Id)).Single();

        var result = await sut.DeleteAsync(created.Id);

        result.Succeeded.Should().BeTrue();
        (await sut.GetAsync(today, today, region.Id)).Should().BeEmpty();
    }

    public void Dispose() => _fixture.Dispose();
}
