using PDV.Domain.Entities;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Services;

/// <summary>
/// Domain service for operations that don't naturally fit inside a single entity,
/// or involve multiple entities/aggregates.
/// </summary>
public class SaleDomainService
{
    public void ApplyDiscount(Sale sale, decimal discountPercentage)
    {
        if (sale == null) throw new ArgumentNullException(nameof(sale));
        
        if (discountPercentage < 0 || discountPercentage > 100)
            throw new DomainException("El porcentaje de descuento debe estar entre 0 y 100.");
        
        if (sale.IsPaid)
            throw new DomainException("No se puede aplicar un descuento a una venta ya pagada.");
            
        // Logic to distribute discount across items or apply at a higher level
        // Currently, Sale lacks a discount property, so this is illustrative
        // of a domain service implementing business rules.
    }
}
