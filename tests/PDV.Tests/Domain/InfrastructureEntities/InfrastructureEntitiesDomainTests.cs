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
        branch.Update("Sucursal Centro Modificada", null, "5550000000", "modificado@correo.com");
        branch.SetAsMainBranch();

        // Assert
        Assert.Equal("Sucursal Centro Modificada", branch.Name);
        Assert.True(branch.IsMainBranch);
        Assert.Single(branch.DomainEvents.OfType<BranchUpdatedEvent>());
        Assert.Single(branch.DomainEvents.OfType<BranchSetAsMainEvent>());
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
}
