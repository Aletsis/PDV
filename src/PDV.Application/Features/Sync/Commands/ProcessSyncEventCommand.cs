using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sync.Dtos;
using PDV.Domain.Entities;

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
            ReferenceHandler = ReferenceHandler.IgnoreCycles
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

            return SyncProcessResult.Ok();
        }
        catch (Exception ex)
        {
            return SyncProcessResult.Fail($"Event processing error: {ex.Message}");
        }
    }
}
