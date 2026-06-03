using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sync.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Events;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sync.Commands;

public record ProcessSyncEventCommand(OutboxSyncDto Dto) : IRequest<SyncProcessResult>;

public record SyncProcessResult(bool Success, string? ErrorMessage)
{
    public static SyncProcessResult Ok() => new(true, null);
    public static SyncProcessResult Fail(string error) => new(false, error);
}

public class ProcessSyncEventCommandHandler : IRequestHandler<ProcessSyncEventCommand, SyncProcessResult>
{
    private readonly IApplicationDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProcessSyncEventCommandHandler(IApplicationDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { ConfigureEntityDeserializationModifier }
            }
        };
    }

    public async Task<SyncProcessResult> Handle(ProcessSyncEventCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        try
        {
            if (dto.EventType.StartsWith("Sale"))
            {
                var sale = JsonSerializer.Deserialize<Sale>(dto.Payload, _jsonOptions);
                if (sale == null) return SyncProcessResult.Fail("Could not deserialize Sale payload.");

                var exists = await _context.Sales.AnyAsync(s => s.Id == sale.Id, cancellationToken);
                if (!exists)
                {
                    _context.Sales.Add(sale);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            else if (dto.EventType.StartsWith("Shift"))
            {
                var shift = JsonSerializer.Deserialize<Shift>(dto.Payload, _jsonOptions);
                if (shift == null) return SyncProcessResult.Fail("Could not deserialize Shift payload.");

                var exists = await _context.Shifts.AnyAsync(s => s.Id == shift.Id, cancellationToken);
                if (!exists)
                {
                    _context.Shifts.Add(shift);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            else if (dto.EventType.StartsWith("Return"))
            {
                var returnEntity = JsonSerializer.Deserialize<Return>(dto.Payload, _jsonOptions);
                if (returnEntity == null) return SyncProcessResult.Fail("Could not deserialize Return payload.");

                var exists = await _context.Returns.AnyAsync(r => r.Id == returnEntity.Id, cancellationToken);
                if (!exists)
                {
                    _context.Returns.Add(returnEntity);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            else if (dto.EventType.StartsWith("CashCut"))
            {
                var cashCut = JsonSerializer.Deserialize<CashCut>(dto.Payload, _jsonOptions);
                if (cashCut == null) return SyncProcessResult.Fail("Could not deserialize CashCut payload.");

                var exists = await _context.CashCuts.AnyAsync(c => c.Id == cashCut.Id, cancellationToken);
                if (!exists)
                {
                    _context.CashCuts.Add(cashCut);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            else if (dto.EventType.StartsWith("Client"))
            {
                var client = JsonSerializer.Deserialize<Client>(dto.Payload, _jsonOptions);
                if (client == null) return SyncProcessResult.Fail("Could not deserialize Client payload.");

                var existing = await _context.Clients.FirstOrDefaultAsync(c => c.Id == client.Id, cancellationToken);
                if (existing == null)
                {
                    _context.Clients.Add(client);
                }
                else
                {
                    existing.UpdateProfile(client.Name, client.TaxId);
                    existing.UpdateContactInfo(client.Phone, client.Email);
                    if (client.Address != null)
                    {
                        existing.UpdateAddress(client.Address);
                    }
                    if (client.IsActive && !existing.IsActive)
                    {
                        existing.Activate();
                    }
                    else if (!client.IsActive && existing.IsActive)
                    {
                        existing.Deactivate();
                    }
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
            else if (dto.EventType.Equals("InventoryMovementRegisteredEvent"))
            {
                var ev = JsonSerializer.Deserialize<InventoryMovementRegisteredEvent>(dto.Payload, _jsonOptions);
                if (ev == null) return SyncProcessResult.Fail("Could not deserialize InventoryMovementRegisteredEvent payload.");

                var exists = await _context.InventoryMovements.AnyAsync(m => m.Id == ev.MovementId, cancellationToken);
                if (!exists)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == ev.ProductId, cancellationToken);
                    if (product != null)
                    {
                        var movement = new InventoryMovement(
                            productId: ev.ProductId,
                            quantity:  ev.Quantity,
                            type:      ev.Type,
                            referenceId: ev.ReferenceId,
                            remarks:   ev.Remarks);
                        movement.SetId(ev.MovementId);

                        _context.InventoryMovements.Add(movement);

                        // Ajustar el stock en el servidor (ev.Quantity es negativo para ventas)
                        product.AdjustStock(product.Stock + ev.Quantity);

                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            return SyncProcessResult.Ok();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            var inner = ex.InnerException;
            while (inner != null)
            {
                msg += " ---> " + inner.Message;
                inner = inner.InnerException;
            }
            return SyncProcessResult.Fail($"Event processing error: {msg}");
        }
    }

    private static void ConfigureEntityDeserializationModifier(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        // 1. Force use of parameterless constructor (even if non-public)
        var ctor = typeInfo.Type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        if (ctor != null)
        {
            typeInfo.CreateObject = () => ctor.Invoke(null);
        }

        var ignoredTypes = new HashSet<Type>
        {
            typeof(Product),
            typeof(Client),
            typeof(Branch),
            typeof(CashRegister),
            typeof(Sale)
        };

        // 2. Enable deserialization of properties with non-public setters or backing fields
        foreach (var property in typeInfo.Properties)
        {
            // Ignore entities navigation properties to prevent EF Core insert constraint conflicts
            if (ignoredTypes.Contains(property.PropertyType))
            {
                property.Set = null;
                continue;
            }

            var underlyingProperty = typeInfo.Type.GetProperty(property.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (underlyingProperty != null)
            {
                // If it has a non-public setter, bind it
                if (property.Set == null && underlyingProperty.SetMethod != null)
                {
                    var setter = underlyingProperty.SetMethod;
                    property.Set = (obj, val) => setter.Invoke(obj, new[] { val });
                }
            }

            // If it is a read-only collection (e.g. backed by _items, _taxes, etc.)
            if (property.Set == null)
            {
                string fieldName = "_" + JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                var field = typeInfo.Type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (field != null)
                {
                    property.Set = (obj, value) =>
                    {
                        var list = field.GetValue(obj);
                        if (list == null)
                        {
                            list = Activator.CreateInstance(field.FieldType);
                            field.SetValue(obj, list);
                        }

                        if (value is System.Collections.IEnumerable enumerable)
                        {
                            var clearMethod = field.FieldType.GetMethod("Clear");
                            clearMethod?.Invoke(list, null);

                            var addMethod = field.FieldType.GetMethod("Add");
                            if (addMethod != null)
                            {
                                foreach (var item in enumerable)
                                {
                                    addMethod.Invoke(list, new[] { item });
                                }
                            }
                        }
                    };
                }
            }
        }
    }
}
