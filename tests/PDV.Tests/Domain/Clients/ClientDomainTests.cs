using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.Clients;

public class ClientDomainTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectlyAndRaisesEvent()
    {
        // Act
        var client = new Client(
            code: "C-001",
            name: "Cliente de Prueba",
            taxId: "XAXX010101000",
            phone: "5551234567",
            email: "test@domain.com",
            clientType: ClientType.Wholesale
        );

        // Assert
        Assert.Equal("C-001", client.Code);
        Assert.Equal("Cliente de Prueba", client.Name);
        Assert.Equal("XAXX010101000", client.TaxId);
        Assert.Equal("5551234567", client.Phone);
        Assert.Equal("test@domain.com", client.Email);
        Assert.Equal(ClientType.Wholesale, client.ClientType);
        Assert.True(client.IsActive);

        var registeredEvent = client.DomainEvents.OfType<ClientRegisteredEvent>().FirstOrDefault();
        Assert.NotNull(registeredEvent);
        Assert.Equal(client.Id, registeredEvent!.ClientId);
        Assert.Equal(client.Name, registeredEvent.Name);
    }

    [Theory]
    [InlineData("", "Nombre", "XAXX010101000", "5551234567", "test@domain.com", "El código del cliente es obligatorio.")]
    [InlineData("C-01", "", "XAXX010101000", "5551234567", "test@domain.com", "El nombre del cliente es obligatorio.")]
    [InlineData("C-01", "Nombre", "ABC", "5551234567", "test@domain.com", "El RFC/TaxId debe tener entre 10 y 13 caracteres.")]
    [InlineData("C-01", "Nombre", "XAXX010101000", "12345", "test@domain.com", "El teléfono debe contener al menos 10 dígitos.")]
    [InlineData("C-01", "Nombre", "XAXX010101000", "5551234567", "invalidemail", "El formato del correo electrónico es inválido.")]
    public void Constructor_WithInvalidParameters_ThrowsDomainException(string code, string name, string taxId, string phone, string email, string expectedMsg)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new Client(code, name, taxId, phone, email));
        Assert.Equal(expectedMsg, exception.Message);
    }

    [Fact]
    public void UpdateProfile_WithValidParameters_UpdatesAndRaisesEvent()
    {
        // Arrange
        var client = new Client("C-01", "Juan Perez", "PEJJ800101XXX", "5551234567", "juan@perez.com");

        // Act
        client.UpdateProfile("Juan Perez Lopez", "PEJJ800101AAA");

        // Assert
        Assert.Equal("Juan Perez Lopez", client.Name);
        Assert.Equal("PEJJ800101AAA", client.TaxId);

        var profileEvent = client.DomainEvents.OfType<ClientProfileUpdatedEvent>().FirstOrDefault();
        Assert.NotNull(profileEvent);
        Assert.Equal(client.Id, profileEvent!.ClientId);
        Assert.Equal("Juan Perez Lopez", profileEvent.Name);
        Assert.Equal("PEJJ800101AAA", profileEvent.TaxId);
    }

    [Fact]
    public void UpdateContactInfo_WithValidParameters_UpdatesAndRaisesEvent()
    {
        // Arrange
        var client = new Client("C-01", "Juan Perez", "PEJJ800101XXX", "5551234567", "juan@perez.com");

        // Act
        client.UpdateContactInfo("5550000000", "nuevo@correo.com");

        // Assert
        Assert.Equal("5550000000", client.Phone);
        Assert.Equal("nuevo@correo.com", client.Email);

        var contactEvent = client.DomainEvents.OfType<ClientContactInfoUpdatedEvent>().FirstOrDefault();
        Assert.NotNull(contactEvent);
        Assert.Equal(client.Id, contactEvent!.ClientId);
        Assert.Equal("5550000000", contactEvent.Phone);
        Assert.Equal("nuevo@correo.com", contactEvent.Email);
    }

    [Fact]
    public void ActivateAndDeactivate_StateTransitionsCorrectlyAndRaisesEvents()
    {
        // Arrange
        var client = new Client("C-01", "Juan Perez", "PEJJ800101XXX", "5551234567", "juan@perez.com");
        Assert.True(client.IsActive);

        // Act - Deactivate
        client.Deactivate();

        // Assert
        Assert.False(client.IsActive);
        var deactivatedEvent = client.DomainEvents.OfType<ClientDeactivatedEvent>().FirstOrDefault();
        Assert.NotNull(deactivatedEvent);
        Assert.Equal(client.Id, deactivatedEvent!.ClientId);

        // Act - Activate
        client.Activate();

        // Assert
        Assert.True(client.IsActive);
        var activatedEvent = client.DomainEvents.OfType<ClientActivatedEvent>().FirstOrDefault();
        Assert.NotNull(activatedEvent);
        Assert.Equal(client.Id, activatedEvent!.ClientId);
    }

    [Fact]
    public void ChangeClientType_RaisesCorrectEvent()
    {
        // Arrange
        var client = new Client("C-01", "Juan Perez", "PEJJ800101XXX", "5551234567", "juan@perez.com", ClientType.Retail);

        // Act
        client.ChangeClientType(ClientType.Wholesale);

        // Assert
        Assert.Equal(ClientType.Wholesale, client.ClientType);
        var typeEvent = client.DomainEvents.OfType<ClientTypeChangedEvent>().FirstOrDefault();
        Assert.NotNull(typeEvent);
        Assert.Equal(client.Id, typeEvent!.ClientId);
        Assert.Equal(ClientType.Wholesale, typeEvent.NewType);
    }

    [Fact]
    public void UpdateAddress_WithValidAddress_UpdatesAndRaisesEvent()
    {
        // Arrange
        var client = new Client("C-01", "Juan Perez", "PEJJ800101XXX", "5551234567", "juan@perez.com");
        var address = Address.Create("Av. Siempre Viva 742", "Springfield", "State", "00000", "EUA");

        // Act
        client.UpdateAddress(address);

        // Assert
        Assert.NotNull(client.Address);
        Assert.Equal("Av. Siempre Viva 742", client.Address!.Street);

        var addressEvent = client.DomainEvents.OfType<ClientAddressUpdatedEvent>().FirstOrDefault();
        Assert.NotNull(addressEvent);
        Assert.Equal(client.Id, addressEvent!.ClientId);
    }
}
