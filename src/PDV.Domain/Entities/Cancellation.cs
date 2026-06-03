using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class Cancellation : BaseEntity, IAggregateRoot
{
    public DateTime CancellationDate { get; private set; }
    public CancellationType Type { get; private set; }
    public string Reason { get; private set; }
    
    public Guid? SaleId { get; private set; }
    public Sale? Sale { get; private set; }
    
    public Guid? SaleItemId { get; private set; }
    public SaleItem? SaleItem { get; private set; }
    
    public string UserId { get; private set; }
    


    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

#pragma warning disable CS8618
    private Cancellation() { } // Para EF Core
#pragma warning restore CS8618

    public Cancellation(
        Guid branchId,
        CancellationType type, 
        string reason, 
        string userId,
        Guid? saleId = null, 
        Guid? saleItemId = null)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Se requiere un motivo para la cancelación.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Se requiere el ID de usuario (autorizador/cajero) de la cancelación.");

        if (type == CancellationType.Sale && !saleId.HasValue)
            throw new DomainException("Para cancelar una venta completa, se debe proporcionar el SaleId.");
            
        if (type == CancellationType.Product && !saleItemId.HasValue)
            throw new DomainException("Para cancelar un producto, se debe proporcionar el SaleItemId.");

        BranchId = branchId;
        Type = type;
        Reason = reason.Trim();
        UserId = userId;
        SaleId = saleId;
        SaleItemId = saleItemId;
        CancellationDate = DateTime.UtcNow;

        AddDomainEvent(new CancellationCreatedEvent(Id, Type, SaleId, SaleItemId, Reason));
    }
}