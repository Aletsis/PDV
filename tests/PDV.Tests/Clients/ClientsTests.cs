using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

        var handler = new CreateClientCommandHandler(context);
        var command = new CreateClientCommand
        {
            Name = "Cliente de Prueba S.A.",
            TaxId = "XAXX010101000",
            Address = "Calle Falsa 123",
            Phone = "5551234567",
            Email = "prueba@cliente.com"
        };

        // Act
        var clientId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, clientId);

        var client = await context.Clients.FindAsync(new object[] { clientId }, CancellationToken.None);
        Assert.NotNull(client);
        Assert.Equal("Cliente de Prueba S.A.", client!.Name);
        Assert.Equal("XAXX010101000", client.TaxId);
        Assert.Equal("Calle Falsa 123", client.Address?.Street);

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

        var existingClient = new Client("Juan Perez", "PEJJ800101XXX", "5559876543", "juan@perez.com");
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateClientCommandHandler(context);
        var command = new UpdateClientCommand
        {
            Id = existingClient.Id,
            Name = "Juan Perez Lopez",
            TaxId = "PEJJ800101AAA",
            Address = "Av. Siempre Viva 742",
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
        Assert.Equal("Juan Perez Lopez", client!.Name);
        Assert.Equal("PEJJ800101AAA", client.TaxId);
        Assert.Equal("Av. Siempre Viva 742", client.Address?.Street);
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

        var clientToDelete = new Client("Eliminar SRL", "ELI000000AAA", "5559998887", "eliminar@correo.com");
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
}
