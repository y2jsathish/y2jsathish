using ATMTicketing.Application.DTOs;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Infrastructure.Services;
using ATMTicketing.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace ATMTicketing.Tests.Services;

public class RegionServiceTests : IDisposable
{
    private readonly TestFixture _fixture = new();

    private RegionService CreateSut() => new(_fixture.UnitOfWork, _fixture.UserManager);

    [Fact]
    public async Task CreateAsync_AddsRegion_VisibleInGetAll()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(new RegionEditDto { RegionName = "Central", Zone = "Zone E" });

        result.Succeeded.Should().BeTrue();
        var all = await sut.GetAllAsync();
        all.Should().ContainSingle(r => r.RegionName == "Central" && r.AtmCount == 0 && r.VendorCount == 0 && r.UserCount == 0);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateName()
    {
        var sut = CreateSut();
        await sut.CreateAsync(new RegionEditDto { RegionName = "North", Zone = "Zone A" });

        var result = await sut.CreateAsync(new RegionEditDto { RegionName = "North", Zone = "Zone Z" });

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReportsAccurateAtmVendorAndUserCounts()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();
        await _fixture.CreateUserAsync("eng@test.local", Roles.FieldEngineer, region.Id);

        var sut = CreateSut();
        var all = await sut.GetAllAsync();

        var north = all.Single(r => r.Id == region.Id);
        north.AtmCount.Should().Be(1);
        north.VendorCount.Should().Be(1);
        north.UserCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_Fails_WhenRegionHasAtmsVendorsOrUsers()
    {
        var (region, _, _, _) = await _fixture.SeedBaselineAsync();

        var sut = CreateSut();
        var result = await sut.DeleteAsync(region.Id);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesUnusedRegion()
    {
        var sut = CreateSut();
        await sut.CreateAsync(new RegionEditDto { RegionName = "Unused", Zone = "Zone X" });
        var region = (await sut.GetAllAsync()).Single(r => r.RegionName == "Unused");

        var result = await sut.DeleteAsync(region.Id);

        result.Succeeded.Should().BeTrue();
        (await sut.GetAllAsync()).Should().NotContain(r => r.Id == region.Id);
    }

    public void Dispose() => _fixture.Dispose();
}
