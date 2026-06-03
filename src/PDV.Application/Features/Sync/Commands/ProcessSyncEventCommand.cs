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
            },
            Converters =
            {
                new DateTimeUtcConverter(),
                new NullableDateTimeUtcConverter()
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

                // 1. Ensure referenced Shift exists to satisfy foreign key constraints (Offline-first Stubbing)
                var shiftExists = await _context.Shifts.AnyAsync(s => s.Id == sale.ShiftId, cancellationToken);
                if (!shiftExists)
                {
                    var registerId = sale.CashRegisterId ?? Guid.Empty;
                    var registerExists = registerId != Guid.Empty && await _context.CashRegisters.AnyAsync(r => r.Id == registerId, cancellationToken);
                    if (!registerExists)
                    {
                        var firstReg = await _context.CashRegisters.FirstOrDefaultAsync(cancellationToken);
                        if (firstReg != null)
                        {
                            registerId = firstReg.Id;
                        }
                        else
                        {
                            var branch = await _context.Branches.FirstOrDefaultAsync(cancellationToken);
                            var branchId = branch?.Id ?? Guid.NewGuid();
                            if (branch == null)
                            {
                                var dummyBranch = new Branch("Sucursal Temporal", "T-001", null, "", null, false);
                                dummyBranch.SetId(branchId);
                                _context.Branches.Add(dummyBranch);
                                await _context.SaveChangesAsync(cancellationToken);
                            }
                            
                            var dummyRegister = new CashRegister("Caja Temporal", "Piso Ventas", branchId);
                            if (sale.CashRegisterId.HasValue && sale.CashRegisterId != Guid.Empty)
                            {
                                dummyRegister.SetId(sale.CashRegisterId.Value);
                                registerId = sale.CashRegisterId.Value;
                            }
                            else
                            {
                                registerId = dummyRegister.Id;
                            }
                            
                            _context.CashRegisters.Add(dummyRegister);
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                    }

                    var placeholderShift = new Shift(registerId, sale.UserId ?? "sync-system", 0);
                    placeholderShift.SetId(sale.ShiftId);
                    
                    _context.Shifts.Add(placeholderShift);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // 2. Ensure referenced Client exists if provided (optional foreign key)
                if (sale.ClientId.HasValue && sale.ClientId != Guid.Empty)
                {
                    var clientExists = await _context.Clients.AnyAsync(c => c.Id == sale.ClientId.Value, cancellationToken);
                    if (!clientExists)
                    {
                        var placeholderClient = new Client("C-TEMP", "Cliente Temporal", "", "", "");
                        placeholderClient.SetId(sale.ClientId.Value);
                        _context.Clients.Add(placeholderClient);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }

                // 3. Ensure referenced Branch exists (required foreign key)
                var branchIdForSale = sale.BranchId;
                var branchExists = branchIdForSale != Guid.Empty && await _context.Branches.AnyAsync(b => b.Id == branchIdForSale, cancellationToken);
                if (!branchExists)
                {
                    var firstBranch = await _context.Branches.FirstOrDefaultAsync(cancellationToken);
                    if (firstBranch != null)
                    {
                        var branchProp = typeof(Sale).GetProperty("BranchId");
                        branchProp?.SetValue(sale, firstBranch.Id);
                    }
                    else
                    {
                        var dummyBranch = new Branch("Sucursal Temporal", "T-001", null, "", null, false);
                        dummyBranch.SetId(branchIdForSale == Guid.Empty ? Guid.NewGuid() : branchIdForSale);
                        
                        if (branchIdForSale == Guid.Empty)
                        {
                            var branchProp = typeof(Sale).GetProperty("BranchId");
                            branchProp?.SetValue(sale, dummyBranch.Id);
                        }
                        
                        _context.Branches.Add(dummyBranch);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }

                // 4. Save/Update the Sale
                var existing = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == sale.Id, cancellationToken);

                if (existing == null)
                {
                    _context.Sales.Add(sale);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    // Actualizar propiedades escalares de la venta
                    ((DbContext)_context).Entry(existing).CurrentValues.SetValues(sale);

                    // Sincronizar colección de artículos (SaleItem)
                    var targetField = typeof(Sale).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (targetField != null)
                    {
                        var targetList = targetField.GetValue(existing) as List<SaleItem>;
                        if (targetList != null)
                        {
                            // A. Remover artículos que ya no están en la venta entrante
                            var itemsToRemove = targetList.Where(item => !sale.Items.Any(i => i.Id == item.Id)).ToList();
                            foreach (var item in itemsToRemove)
                            {
                                targetList.Remove(item);
                                _context.SaleItems.Remove(item);
                            }

                            // B. Actualizar existentes y agregar nuevos
                            foreach (var incomingItem in sale.Items)
                            {
                                var existingItem = targetList.FirstOrDefault(i => i.Id == incomingItem.Id);
                                if (existingItem != null)
                                {
                                    ((DbContext)_context).Entry(existingItem).CurrentValues.SetValues(incomingItem);
                                }
                                else
                                {
                                    // Vincular explícitamente el artículo a la venta existente
                                    var saleIdProp = typeof(SaleItem).GetProperty("SaleId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    saleIdProp?.SetValue(incomingItem, existing.Id);
                                    
                                    targetList.Add(incomingItem);
                                }
                            }
                        }
                    }

                    // Copiar desglose de impuestos (owned types)
                    CopyPrivateCollection(sale, existing, "_taxes");

                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            else if (dto.EventType.StartsWith("Shift"))
            {
                var shift = JsonSerializer.Deserialize<Shift>(dto.Payload, _jsonOptions);
                if (shift == null) return SyncProcessResult.Fail("Could not deserialize Shift payload.");

                var existing = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == shift.Id, cancellationToken);
                if (existing == null)
                {
                    _context.Shifts.Add(shift);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    // Update existing placeholder shift with actual synced values
                    ((DbContext)_context).Entry(existing).CurrentValues.SetValues(shift);
                    
                    CopyPrivateCollection(shift, existing, "_paymentMethodTotals");
                    CopyPrivateCollection(shift, existing, "_salesTaxTotals");
                    CopyPrivateCollection(shift, existing, "_returnsTaxTotals");
                    CopyPrivateCollection(shift, existing, "_creditNotes");
                    
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
        catch (DbUpdateConcurrencyException concurrencyEx)
        {
            var entityDetails = string.Join(", ", concurrencyEx.Entries.Select(e => 
            {
                var type = e.Entity.GetType().Name;
                var state = e.State.ToString();
                var keys = string.Join("&", e.Metadata.FindPrimaryKey()?.Properties
                    .Select(p => $"{p.Name}={e.Property(p.Name).CurrentValue}") ?? Array.Empty<string>());
                return $"{type} (State: {state}, Keys: {keys})";
            }));
            return SyncProcessResult.Fail($"Event processing concurrency error: {concurrencyEx.Message}. Entities: {entityDetails}");
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

    private static void CopyPrivateCollection(object source, object target, string fieldName)
    {
        var field = source.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            var srcList = field.GetValue(source) as System.Collections.IList;
            var targetList = field.GetValue(target) as System.Collections.IList;
            if (srcList != null && targetList != null)
            {
                targetList.Clear();
                foreach (var item in srcList)
                {
                    targetList.Add(item);
                }
            }
        }
    }
}

public class DateTimeUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Unspecified 
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) 
            : dt.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("o"));
    }
}

public class NullableDateTimeUtcConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Unspecified 
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) 
            : dt.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("o"));
        else
            writer.WriteNullValue();
    }
}
