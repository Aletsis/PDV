using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Application.Features.Sync.Commands;
using PDV.Application.Features.Sync.Dtos;
using PDV.Domain.Common;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Infrastructure.Persistence;
using Xunit;

namespace PDV.Tests.Sync;

public class SyncReplicationTests
{
    private DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Sync_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private void SetLastModifiedAt(BaseEntity entity, DateTime? value)
    {
        var prop = typeof(BaseEntity).GetProperty("LastModifiedAt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        prop?.SetValue(entity, value);
    }

    [Fact]
    public async Task Handle_SyncNewClient_InsertsClient()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);
        var handler = new ProcessSyncEventCommandHandler(context);

        var clientId = Guid.NewGuid();
        var clientPayload = new Client("C-001", "Client One", "XAXX010101000", "5551234567", "client1@email.com");
        clientPayload.SetId(clientId);

        var payloadJson = JsonSerializer.Serialize(clientPayload);
        var dto = new OutboxSyncDto(Guid.NewGuid(), "ClientRegisteredEvent", payloadJson, DateTime.UtcNow);
        var command = new ProcessSyncEventCommand(dto);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        
        var clientInDb = await context.Clients.FindAsync(clientId);
        Assert.NotNull(clientInDb);
        Assert.Equal("Client One", clientInDb.Name);
        Assert.Equal("C-001", clientInDb.Code);
    }

    [Fact]
    public async Task Handle_SyncClientNoConflict_UpdatesClient()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);
        
        var clientId = Guid.NewGuid();
        var existingClient = new Client("C-001", "Client Original", "XAXX010101000", "5551234567", "client1@email.com");
        existingClient.SetId(clientId);
        SetLastModifiedAt(existingClient, DateTime.UtcNow.AddHours(-2)); // Modified 2 hours ago on server
        
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var handler = new ProcessSyncEventCommandHandler(context);

        // Incoming client modified 1 hour ago (newer than 2 hours ago)
        var clientPayload = new Client("C-001", "Client Updated", "XAXX010101000", "5551234567", "client1@email.com");
        clientPayload.SetId(clientId);
        SetLastModifiedAt(clientPayload, DateTime.UtcNow.AddHours(-1));

        var payloadJson = JsonSerializer.Serialize(clientPayload);
        var dto = new OutboxSyncDto(Guid.NewGuid(), "ClientProfileUpdatedEvent", payloadJson, DateTime.UtcNow);
        var command = new ProcessSyncEventCommand(dto);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        
        var clientInDb = await context.Clients.FindAsync(clientId);
        Assert.NotNull(clientInDb);
        Assert.Equal("Client Updated", clientInDb.Name); // Update succeeded
        
        var conflicts = await context.SyncConflicts.ToListAsync();
        Assert.Empty(conflicts); // No conflicts recorded
    }

    [Fact]
    public async Task Handle_SyncClientWithConflict_LogsConflictAndKeepsServerVersion()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);
        
        var clientId = Guid.NewGuid();
        var existingClient = new Client("C-001", "Server Newer Version", "XAXX010101000", "5551234567", "client1@email.com");
        existingClient.SetId(clientId);
        SetLastModifiedAt(existingClient, DateTime.UtcNow.AddHours(-1)); // Modified 1 hour ago on server
        
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var handler = new ProcessSyncEventCommandHandler(context);

        // Incoming client modified 2 hours ago (older than 1 hour ago)
        var clientPayload = new Client("C-001", "Client Older Version", "XAXX010101000", "5551234567", "client1@email.com");
        clientPayload.SetId(clientId);
        SetLastModifiedAt(clientPayload, DateTime.UtcNow.AddHours(-2));

        var payloadJson = JsonSerializer.Serialize(clientPayload);
        var dto = new OutboxSyncDto(Guid.NewGuid(), "ClientProfileUpdatedEvent", payloadJson, DateTime.UtcNow);
        var command = new ProcessSyncEventCommand(dto);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success); // returns Success to acknowledge the sync and clean client queue
        
        // Ensure server's data remains unmodified
        var clientInDb = await context.Clients.FindAsync(clientId);
        Assert.NotNull(clientInDb);
        Assert.Equal("Server Newer Version", clientInDb.Name);

        // Validate that a conflict record was written to the DB
        var conflicts = await context.SyncConflicts.ToListAsync();
        Assert.Single(conflicts);
        
        var conflict = conflicts.First();
        Assert.Equal("Client", conflict.EntityName);
        Assert.Equal(clientId, conflict.EntityId);
        Assert.Equal("ConcurrentWriteOutOfSync", conflict.ConflictType);
        Assert.False(conflict.Resolved);
        Assert.Contains("Client Older Version", conflict.ClientValuesJson);
        Assert.Contains("Server Newer Version", conflict.ServerValuesJson);
    }
}
