using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Branches.Commands.CreateBranch;
using PDV.Application.Features.Branches.Commands.UpdateBranch;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;

namespace PDV.Tests.Branches;

public class BranchesCommandHandlerTests
{
    private DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Branches_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    [Fact]
    public async Task Handle_CreateBranch_WithExplicitCoordinates_SavesCorrectly()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);
        var repository = new BranchRepository(context);
        var mockGeocodingService = new Mock<IGeocodingService>();

        var handler = new CreateBranchCommandHandler(repository, context, mockGeocodingService.Object);
        var command = new CreateBranchCommand(
            Name: "Sucursal Chapultepec",
            Code: "SUC-CHAP",
            Street: "Av Chapultepec",
            Phone: "3331234567",
            ExteriorNumber: "450",
            InteriorNumber: "A",
            Colony: "Americana",
            ZipCode: "44160",
            City: "Guadalajara",
            State: "Jalisco",
            Country: "México",
            Email: "chapultepec@tienda.com",
            IsMainBranch: false,
            Latitude: 20.6736,
            Longitude: -103.3682
        );

        // Act
        var branchId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, branchId);
        var branch = await context.Branches.FindAsync(new object[] { branchId }, CancellationToken.None);
        Assert.NotNull(branch);
        Assert.Equal("SUC-CHAP", branch!.Code);
        Assert.Equal("Sucursal Chapultepec", branch.Name);
        Assert.Equal("Av Chapultepec", branch.Address?.Street);
        Assert.Equal("450", branch.Address?.ExteriorNumber);
        Assert.Equal("Americana", branch.Address?.Colony);
        Assert.Equal("44160", branch.Address?.ZipCode);
        Assert.Equal(20.6736, branch.Latitude);
        Assert.Equal(-103.3682, branch.Longitude);

        // Geocoding service shouldn't be called when coordinates are provided explicitly
        mockGeocodingService.Verify(g => g.GeocodeAddressQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CreateBranch_WithoutCoordinates_CallsGeocodingFallback()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);
        var repository = new BranchRepository(context);
        var mockGeocodingService = new Mock<IGeocodingService>();

        mockGeocodingService
            .Setup(g => g.GeocodeAddressQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((20.6800, -103.3500));

        var handler = new CreateBranchCommandHandler(repository, context, mockGeocodingService.Object);
        var command = new CreateBranchCommand(
            Name: "Sucursal Centro",
            Code: "SUC-CENTRO",
            Street: "Av 16 de Septiembre",
            Phone: "3331234567",
            ExteriorNumber: "100",
            Colony: "Centro",
            ZipCode: "44100",
            City: "Guadalajara",
            State: "Jalisco",
            Country: "México"
        );

        // Act
        var branchId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, branchId);
        var branch = await context.Branches.FindAsync(new object[] { branchId }, CancellationToken.None);
        Assert.NotNull(branch);
        Assert.Equal(20.6800, branch!.Latitude);
        Assert.Equal(-103.3500, branch.Longitude);

        mockGeocodingService.Verify(g => g.GeocodeAddressQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateBranch_UpdatesAddressAndCoordinates()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);
        var repository = new BranchRepository(context);
        var mockGeocodingService = new Mock<IGeocodingService>();

        var existingBranch = new Branch("Sucursal Original", "SUC-01", null, "3331112233");
        context.Branches.Add(existingBranch);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateBranchCommandHandler(repository, mockGeocodingService.Object);
        var updateCommand = new UpdateBranchCommand(
            Id: existingBranch.Id,
            Name: "Sucursal Actualizada",
            Street: "Av Vallarta",
            Phone: "3339998877",
            ExteriorNumber: "1234",
            Colony: "Americana",
            ZipCode: "44160",
            City: "Guadalajara",
            State: "Jalisco",
            Country: "México",
            Email: "vallarta@tienda.com",
            Latitude: 20.6740,
            Longitude: -103.3700
        );

        // Act
        await handler.Handle(updateCommand, CancellationToken.None);

        // Assert
        var updated = await context.Branches.FindAsync(new object[] { existingBranch.Id }, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Sucursal Actualizada", updated!.Name);
        Assert.Equal("Av Vallarta", updated.Address?.Street);
        Assert.Equal("1234", updated.Address?.ExteriorNumber);
        Assert.Equal(20.6740, updated.Latitude);
        Assert.Equal(-103.3700, updated.Longitude);
    }

    [Fact]
    public async Task Handle_CreateBranch_WithOrderSeries_SavesAndCalculatesEffectiveSeries()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);
        var repository = new BranchRepository(context);
        var mockGeocodingService = new Mock<IGeocodingService>();

        var handler = new CreateBranchCommandHandler(repository, context, mockGeocodingService.Object);
        var command = new CreateBranchCommand(
            Name: "Sucursal Periférico",
            Code: "SUC-PERI",
            Street: "Periférico Sur",
            Phone: "3331112233",
            OrderSeries: "PED-SUR"
        );

        // Act
        var branchId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var branch = await context.Branches.FindAsync(new object[] { branchId }, CancellationToken.None);
        Assert.NotNull(branch);
        Assert.Equal("PED-SUR", branch!.OrderSeries);
        Assert.Equal("PED-SUR", branch.GetEffectiveOrderSeries());
    }

    [Fact]
    public void Branch_WithoutExplicitOrderSeries_ReturnsDefaultPrefixedCode()
    {
        var branch = new Branch("Sucursal Norte", "NOR01", null, "3331112233");
        Assert.Null(branch.OrderSeries);
        Assert.Equal("PED-NOR01", branch.GetEffectiveOrderSeries());
    }
}
