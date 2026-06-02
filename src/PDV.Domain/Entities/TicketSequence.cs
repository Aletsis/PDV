using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz que controla la secuencia de números de notas internas por caja y tipo de documento.
/// Un registro por combinación (CashRegister + TicketSequenceType).
///
/// Análogo a FolioSequence pero con scope de CashRegister (no Branch)
/// y sin regulación del SAT — son documentos operativos internos.
///
/// CONCURRENCIA: El repositorio debe usar UPDLOCK al leer este agregado
/// para garantizar unicidad del número de nota dentro de cada caja.
/// </summary>
public class TicketSequence : BaseEntity, IAggregateRoot
{
    public Guid CashRegisterId { get; private set; }
    public CashRegister? CashRegister { get; private set; }

    public TicketSequenceType SequenceType { get; private set; }

    /// <summary>Último número de nota emitido. Inicia en 0.</summary>
    public int LastTicketNumber { get; private set; }

    /// <summary>
    /// Prefijo que identifica esta secuencia en los documentos impresos.
    /// Ej: "C1V" → nota C1V000001. Si es null, se imprime solo el número.
    /// </summary>
    public string? Series { get; private set; }

    /// <summary>
    /// Si true, la secuencia se reinicia a 0 al abrir cada nuevo turno.
    /// Útil para cajas que numeran sus tickets por turno (T001, T002…).
    /// Si false, el contador es perpetuo.
    /// </summary>
    public bool ResetOnNewShift { get; private set; }

    /// <summary>
    /// Tipos de secuencia que reinician su contador al abrir un nuevo turno.
    /// Regla de negocio: las recolecciones de efectivo se numeran por turno,
    /// no de forma perpetua, para facilitar el arqueo del turno activo.
    /// </summary>
    private static readonly HashSet<TicketSequenceType> ShiftResettableTypes = new()
    {
        TicketSequenceType.CashCollection
    };

    /// <summary>Token de concurrencia optimista.</summary>
    public byte[]? RowVersion { get; set; }

#pragma warning disable CS8618
    private TicketSequence() { } // Para EF Core
#pragma warning restore CS8618

    public TicketSequence(Guid cashRegisterId, TicketSequenceType sequenceType, string? series = null)
    {
        if (cashRegisterId == Guid.Empty) throw new DomainException("El ID de caja es requerido.");

        CashRegisterId = cashRegisterId;
        SequenceType = sequenceType;
        Series = series?.Trim().ToUpperInvariant();
        LastTicketNumber = 0;

        // Regla de negocio: el dominio decide quién reinicia, no el caller.
        ResetOnNewShift = ShiftResettableTypes.Contains(sequenceType);

        AddDomainEvent(new TicketSequenceCreatedEvent(Id, CashRegisterId, SequenceType));
    }

    /// <summary>
    /// Obtiene e incrementa el siguiente número de nota de forma atómica.
    /// El repositorio DEBE haber adquirido UPDLOCK antes de llamar a este método.
    /// </summary>
    public int GetNextTicketNumber()
    {
        LastTicketNumber++;

        AddDomainEvent(new TicketIssuedEvent(Id, CashRegisterId, SequenceType, LastTicketNumber));

        return LastTicketNumber;
    }

    /// <summary>
    /// Reinicia la secuencia al abrir un nuevo turno.
    /// Solo aplica si ResetOnNewShift = true.
    /// </summary>
    public void ResetForNewShift()
    {
        if (!ResetOnNewShift)
            throw new DomainException($"La secuencia de {SequenceType} no está configurada para reiniciarse por turno.");

        LastTicketNumber = 0;
    }

    /// <summary>
    /// Corrección manual de la secuencia (migración, auditoría).
    /// </summary>
    public void ResetTo(int number)
    {
        if (number < 0) throw new DomainException("El número de nota no puede ser negativo.");
        LastTicketNumber = number;
    }

    /// <summary>
    /// Actualiza el prefijo o serie de la secuencia.
    /// </summary>
    public void UpdateSeries(string? series)
    {
        Series = series?.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Devuelve el último ticket formateado: Serie (si existe) + número.
    /// Ej: Series="C1V", LastTicketNumber=847 → "C1V000847".
    /// </summary>
    public string FormatTicket(int ticketNumber)
    {
        var number = ticketNumber.ToString().PadLeft(6, '0');
        return Series is null ? number : $"{Series}{number}";
    }
}
