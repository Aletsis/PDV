using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Clients.Commands.CreateClient;
using PDV.Application.Features.Clients.Commands.DeleteClient;
using PDV.Application.Features.Clients.Commands.UpdateClient;
using PDV.Domain.Entities;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace PDV.Tests.Clients;

public class ClientsTests
{
    private DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Clients_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;
    }

    [Fact]
    public async Task Handle_CreateClient_SavesToDatabaseAndGeneratesOutbox()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var mockSyncService = new Mock<IComercialApiSyncService>();
        var mockGeocodingService = new Mock<IGeocodingService>();
        var handler = new CreateClientCommandHandler(context, mockSyncService.Object, mockGeocodingService.Object);
        var command = new CreateClientCommand
        {
            Code = "C001",
            Name = "Cliente de Prueba S.A.",
            TaxId = "XAXX010101000",
            Street = "Calle Falsa",
            ExteriorNumber = "123",
            InteriorNumber = "2",
            Colony = "Centro",
            ZipCode = "06000",
            City = "CDMX",
            State = "CDMX",
            Country = "México",
            Phone = "5551234567",
            Email = "prueba@cliente.com"
        };

        // Act
        var clientId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, clientId);

        var client = await context.Clients.FindAsync(new object[] { clientId }, CancellationToken.None);
        Assert.NotNull(client);
        Assert.Equal("C001", client!.Code);
        Assert.Equal("Cliente de Prueba S.A.", client!.Name);
        Assert.Equal("XAXX010101000", client.TaxId);
        Assert.Equal("Calle Falsa", client.Address?.Street);
        Assert.Equal("123", client.Address?.ExteriorNumber);
        Assert.Equal("2", client.Address?.InteriorNumber);
        Assert.Equal("Centro", client.Address?.Colony);
        Assert.Equal("06000", client.Address?.ZipCode);
        Assert.Equal("CDMX", client.Address?.City);
        Assert.Equal("CDMX", client.Address?.State);
        Assert.Equal("México", client.Address?.Country);

        // Validar que se registró el OutboxMessage con el nombre del evento correcto
        var outboxMessage = await context.OutboxMessages
            .FirstOrDefaultAsync(o => o.EventType == "ClientRegisteredEvent", CancellationToken.None);
        Assert.NotNull(outboxMessage);
        Assert.Contains(clientId.ToString(), outboxMessage!.Payload);
    }

    [Fact]
    public async Task Handle_UpdateClient_UpdatesDetailsAndGeneratesOutbox()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var existingClient = new Client("C001", "Juan Perez", "PEJJ800101XXX", "5559876543", "juan@perez.com");
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync(CancellationToken.None);

        var mockSyncService = new Mock<IComercialApiSyncService>();
        var mockGeocodingService = new Mock<IGeocodingService>();
        var handler = new UpdateClientCommandHandler(context, mockSyncService.Object, mockGeocodingService.Object);
        var command = new UpdateClientCommand
        {
            Id = existingClient.Id,
            Code = "C001-Updated",
            Name = "Juan Perez Lopez",
            TaxId = "PEJJ800101AAA",
            Street = "Av. Siempre Viva",
            ExteriorNumber = "742",
            Colony = "Springfield Norte",
            ZipCode = "44100",
            City = "Guadalajara",
            State = "Jalisco",
            Country = "México",
            Phone = "5550000000",
            Email = "juan.perez@correo.com",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var client = await context.Clients.FindAsync(new object[] { existingClient.Id }, CancellationToken.None);
        Assert.NotNull(client);
        Assert.Equal("C001-Updated", client!.Code);
        Assert.Equal("Juan Perez Lopez", client!.Name);
        Assert.Equal("PEJJ800101AAA", client.TaxId);
        Assert.Equal("Av. Siempre Viva", client.Address?.Street);
        Assert.Equal("742", client.Address?.ExteriorNumber);
        Assert.Equal("Springfield Norte", client.Address?.Colony);
        Assert.Equal("44100", client.Address?.ZipCode);
        Assert.Equal("Guadalajara", client.Address?.City);
        Assert.Equal("Jalisco", client.Address?.State);
        Assert.Equal("juan.perez@correo.com", client.Email);

        // Validar outbox del evento Update correcto
        var outboxMessage = await context.OutboxMessages
            .FirstOrDefaultAsync(o => o.EventType == "ClientProfileUpdatedEvent", CancellationToken.None);
        Assert.NotNull(outboxMessage);
        Assert.Contains(existingClient.Id.ToString(), outboxMessage!.Payload);
    }

    [Fact]
    public async Task Handle_DeleteClient_DeactivatesClient()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var clientToDelete = new Client("C002", "Eliminar SRL", "ELI000000AAA", "5559998887", "eliminar@correo.com");
        context.Clients.Add(clientToDelete);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteClientCommandHandler(context);
        var command = new DeleteClientCommand(clientToDelete.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var client = await context.Clients.FindAsync(new object[] { clientToDelete.Id }, CancellationToken.None);
        Assert.NotNull(client);
        Assert.False(client!.IsActive);

        // Validar que se generó evento de desactivación
        var outboxMessage = await context.OutboxMessages
            .FirstOrDefaultAsync(o => o.EventType == "ClientDeactivatedEvent", CancellationToken.None);
        Assert.NotNull(outboxMessage);
    }

    [Fact]
    public async Task Handle_CreateClient_WithoutTaxIdAndFiscalData_SavesSuccessfully()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var mockSyncService = new Mock<IComercialApiSyncService>();
        var mockGeocodingService = new Mock<IGeocodingService>();
        var handler = new CreateClientCommandHandler(context, mockSyncService.Object, mockGeocodingService.Object);
        var command = new CreateClientCommand
        {
            Code = "C-NO-RFC",
            Name = "Cliente Sin RFC",
            TaxId = "",
            Phone = "5551234567",
            Email = "sinrfc@correo.com",
            FiscalRegime = null,
            CfdiUse = null
        };

        // Act
        var clientId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, clientId);

        var client = await context.Clients.FindAsync(new object[] { clientId }, CancellationToken.None);
        Assert.NotNull(client);
        Assert.Equal("C-NO-RFC", client!.Code);
        Assert.Equal("Cliente Sin RFC", client.Name);
        Assert.Equal(string.Empty, client.TaxId);
        Assert.Null(client.FiscalRegime);
        Assert.Null(client.FiscalZipCode);
        Assert.Null(client.CfdiUse);
    }

    [Fact]
    public async Task Handle_UpdateClient_RemovingTaxId_UpdatesSuccessfully()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var existingClient = new Client("C-WITH-RFC", "Cliente Inicial", "XAXX010101000", "5559876543", "test@correo.com");
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync(CancellationToken.None);

        var mockSyncService = new Mock<IComercialApiSyncService>();
        var mockGeocodingService = new Mock<IGeocodingService>();
        var handler = new UpdateClientCommandHandler(context, mockSyncService.Object, mockGeocodingService.Object);
        var command = new UpdateClientCommand
        {
            Id = existingClient.Id,
            Code = "C-WITH-RFC",
            Name = "Cliente Inicial Modificado",
            TaxId = "", // Limpieza de RFC
            FiscalRegime = null,
            CfdiUse = null,
            Phone = "5559876543",
            Email = "test@correo.com",
            IsActive = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);

        var client = await context.Clients.FindAsync(new object[] { existingClient.Id }, CancellationToken.None);
        Assert.NotNull(client);
        Assert.Equal(string.Empty, client!.TaxId);
        Assert.Null(client.FiscalRegime);
        Assert.Null(client.FiscalZipCode);
        Assert.Null(client.CfdiUse);
    }

    [Fact]
    public void CreateClientCommandValidator_AllowsEmptyTaxId()
    {
        var validator = new CreateClientCommandValidator();
        var command = new CreateClientCommand
        {
            Code = "C001",
            Name = "Cliente Mostrador",
            TaxId = ""
        };

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateClientCommandValidator_AllowsEmptyTaxId()
    {
        var validator = new UpdateClientCommandValidator();
        var command = new UpdateClientCommand
        {
            Id = Guid.NewGuid(),
            Code = "C001",
            Name = "Cliente Mostrador",
            TaxId = ""
        };

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
