using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.ValueObjects;

public record CashDenomination
{
    public DenominationType Type { get; init; }
    public int Quantity { get; init; }
    
    public decimal TotalValue => Quantity * Type.GetValue();

    public CashDenomination(DenominationType type, int quantity)
    {
        if (quantity < 0) throw new DomainException("La cantidad de billetes/monedas no puede ser negativa.");
        
        Type = type;
        Quantity = quantity;
    }
}
