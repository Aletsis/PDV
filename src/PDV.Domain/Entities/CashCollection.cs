using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

/// <summary>
/// Entidad que representa una recolección de efectivo (retiro de caja)
/// </summary>
public class CashCollection : BaseEntity, IAggregateRoot
{
    private readonly List<CashDenomination> _denominations = new();

    public DateTime CollectionDate { get; private set; }
    
    public decimal Amount { get; private set; }
    public string Reason { get; private set; }

    public Guid ShiftId { get; private set; }
    public Shift? Shift { get; private set; }
    
    public string UserId { get; private set; }
    

    
    public Guid CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }

    public IReadOnlyCollection<CashDenomination> Denominations => _denominations.AsReadOnly();

#pragma warning disable CS8618
    private CashCollection() { } // Para EF Core
#pragma warning restore CS8618

    public CashCollection(
        Guid shiftId,
        Guid cashRegisterId,
        string userId,
        IEnumerable<CashDenomination> denominations,
        string reason)
    {
        if (shiftId == Guid.Empty) throw new DomainException("El ID del turno es requerido.");
        if (cashRegisterId == Guid.Empty) throw new DomainException("El ID de caja es requerido.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("El ID de usuario es requerido.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("La razón de la recolección es requerida.");

        var denominationsList = denominations?.ToList() ?? new List<CashDenomination>();
        if (!denominationsList.Any()) throw new DomainException("Se debe proporcionar al menos una denominación de efectivo.");

        ShiftId = shiftId;
        CashRegisterId = cashRegisterId;
        UserId = userId;
        Reason = reason.Trim();
        CollectionDate = DateTime.UtcNow;

        _denominations.AddRange(denominationsList);
        Amount = _denominations.Sum(d => d.TotalValue);

        if (Amount <= 0) throw new DomainException("El monto total de la recolección debe ser mayor a cero.");

        AddDomainEvent(new CashCollectedEvent(Id, ShiftId, Amount, Reason));
    }
}