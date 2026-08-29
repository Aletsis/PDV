using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.InfrastructureEntities;

public class InfrastructureEntitiesDomainTests
{
    [Fact]
    public void Branch_Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "Mexico");

        // Act
        var branch = new Branch(
            name: "Sucursal Centro",
            code: "SC01",
            address: address,
            phone: "5551234567",
            email: "centro@sucursal.com",
            isMainBranch: true
        );

        // Assert
        Assert.Equal("Sucursal Centro", branch.Name);
        Assert.Equal("SC01", branch.Code);
        Assert.Equal(address, branch.Address);
        Assert.Equal("5551234567", branch.Phone);
        Assert.Equal("centro@sucursal.com", branch.Email);
        Assert.True(branch.IsMainBranch);
        Assert.True(branch.IsActive);

        var createdEvent = branch.DomainEvents.OfType<BranchCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal(branch.Id, createdEvent!.BranchId);
    }

    [Fact]
    public void Branch_Constructor_WithInvalidNameOrPhone_ThrowsDomainException()
    {
        // Act & Assert - Nombre vacío
        Assert.Throws<DomainException>(() => new Branch("", "SC01", null, "5551234567"));

        // Act & Assert - Telefono invalido
        Assert.Throws<DomainException>(() => new Branch("Sucursal", "SC01", null, "123"));
    }

    [Fact]
    public void Branch_DeactivateMainBranch_ThrowsDomainException()
    {
        // Arrange
        var branch = new Branch("Sucursal", "SC01", null, "5551234567", isMainBranch: true);

        // Act & Assert - No se puede desactivar la sucursal principal
        var exception = Assert.Throws<DomainException>(() => branch.Deactivate());
        Assert.Equal("No se puede desactivar la sucursal principal.", exception.Message);
    }

    [Fact]
    public void Branch_UpdateAndSetAsMain_WorksCorrectly()
    {
        // Arrange
        var branch = new Branch("Sucursal Centro", "SC01", null, "5551234567", isMainBranch: false);

        // Act
        branch.Update("Sucursal Centro Modificada", null, "5550000000", "modificado@correo.com", 20.6736, -103.3682);
        branch.SetAsMainBranch();

        // Assert
        Assert.Equal("Sucursal Centro Modificada", branch.Name);
        Assert.Equal(20.6736, branch.Latitude);
        Assert.Equal(-103.3682, branch.Longitude);
        Assert.True(branch.IsMainBranch);
        Assert.Single(branch.DomainEvents.OfType<BranchUpdatedEvent>());
        Assert.Single(branch.DomainEvents.OfType<BranchSetAsMainEvent>());
    }

    [Fact]
    public void Branch_Constructor_WithCoordinates_InitializesCorrectly()
    {
        // Arrange
        var address = Address.Create("Av Juárez 100", "Guadalajara", "Jalisco", "44100", "México", "100", "A", "Centro");

        // Act
        var branch = new Branch(
            name: "Sucursal Juárez",
            code: "SJ01",
            address: address,
            phone: "3331234567",
            email: "juarez@tienda.com",
            isMainBranch: false,
            latitude: 20.6750,
            longitude: -103.3500
        );

        // Assert
        Assert.Equal(20.6750, branch.Latitude);
        Assert.Equal(-103.3500, branch.Longitude);
        Assert.Equal("Centro", branch.Address?.Colony);
        Assert.Equal("100", branch.Address?.ExteriorNumber);
    }

    [Fact]
    public void Branch_SetCoordinates_UpdatesCoordinates()
    {
        // Arrange
        var branch = new Branch("Sucursal Norte", "SN01", null, "3331234567");

        // Act
        branch.SetCoordinates(20.7000, -103.4000);

        // Assert
        Assert.Equal(20.7000, branch.Latitude);
        Assert.Equal(-103.4000, branch.Longitude);

        // Act - Reset coordinates
        branch.SetCoordinates(null, null);
        Assert.Null(branch.Latitude);
        Assert.Null(branch.Longitude);
    }

    [Fact]
    public void CashRegister_Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var register = new CashRegister("Caja A", "Pasillo 1", Guid.NewGuid(), CashRegisterMode.SalesFloor);

        // Assert
        Assert.Equal("Caja A", register.Name);
        Assert.Equal("Pasillo 1", register.Location);
        Assert.Equal(CashRegisterMode.SalesFloor, register.Mode);
        Assert.True(register.IsActive);
        Assert.Null(register.IpAddress);

        Assert.Single(register.DomainEvents.OfType<CashRegisterCreatedEvent>());
    }

    [Fact]
    public void CashRegister_BindToIp_WithValidIp_BindsSuccessfully()
    {
        // Arrange
        var register = new CashRegister("Caja A", "Pasillo 1", Guid.NewGuid());

        // Act
        register.BindToIp("192.168.1.100");

        // Assert
        Assert.Equal("192.168.1.100", register.IpAddress);
        Assert.Single(register.DomainEvents.OfType<CashRegisterIpBoundEvent>());

        // Act - Unbind
        register.BindToIp(null);
        Assert.Null(register.IpAddress);
    }

    [Fact]
    public void CashRegister_BindToIp_WithInvalidIp_ThrowsDomainException()
    {
        // Arrange
        var register = new CashRegister("Caja A", "Pasillo 1", Guid.NewGuid());

        // Act & Assert
        Assert.Throws<DomainException>(() => register.BindToIp("999.999.999.999"));
        Assert.Throws<DomainException>(() => register.BindToIp("not-an-ip"));
    }

    [Fact]
    public void CashRegister_DeactivateAndActivate_TransitionsState()
    {
        // Arrange
        var register = new CashRegister("Caja A", "Pasillo 1", Guid.NewGuid());

        // Act
        register.Deactivate();

        // Assert
        Assert.False(register.IsActive);

        register.Activate();
        Assert.True(register.IsActive);
    }

    [Fact]
    public void CashRegister_ChangeMode_EmitsCashRegisterModeChangedEvent()
    {
        // Arrange
        var register = new CashRegister("Caja Principal", "Mostrador", Guid.NewGuid(), CashRegisterMode.SalesFloor);

        // Act
        register.ChangeMode(CashRegisterMode.Orders);

        // Assert
        Assert.Equal(CashRegisterMode.Orders, register.Mode);
        var modeEvent = register.DomainEvents.OfType<CashRegisterModeChangedEvent>().FirstOrDefault();
        Assert.NotNull(modeEvent);
        Assert.Equal(CashRegisterMode.Orders, modeEvent!.Mode);
    }

    [Fact]
    public void CashRegister_AssignPrinter_EmitsCashRegisterPrinterAssignedEvent()
    {
        // Arrange
        var register = new CashRegister("Caja Principal", "Mostrador", Guid.NewGuid());
        var printerId = Guid.NewGuid();

        // Act
        register.AssignPrinter(printerId);

        // Assert
        Assert.Equal(printerId, register.AssignedPrinterId);
        var printerEvent = register.DomainEvents.OfType<CashRegisterPrinterAssignedEvent>().FirstOrDefault();
        Assert.NotNull(printerEvent);
        Assert.Equal(printerId, printerEvent!.PrinterId);
    }
}

