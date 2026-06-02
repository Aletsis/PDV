using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz que representa un CFDI de Ingreso.
/// Abarca tanto facturas a clientes individuales como facturas globales de turno.
/// </summary>
public class Invoice : BaseEntity, IAggregateRoot
{
    private readonly List<TaxBreakdown> _taxBreakdowns = new();

    // ──────────────────────────────────────────────
    // Tipo e Identificación Fiscal
    // ──────────────────────────────────────────────
    public InvoiceType Type { get; private set; }
    public InvoiceStatus Status { get; private set; }

    public string Series { get; private set; }
    public string Folio { get; private set; }

    public bool IsGlobal => Type == InvoiceType.Global;
    public string InvoiceNumber => $"{Series}{Folio}";
    public decimal Tax => TotalTax;

    /// <summary>UUID asignado por el PAC al timbrar. Nulo mientras esté en Draft.</summary>
    public string? Uuid { get; private set; }

    public DateTime InvoiceDate { get; private set; }
    public DateTime? StampedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public SatCancellationMotif? SatCancellationMotif { get; private set; }
    public string? SubstituteUuid { get; private set; }

    // ──────────────────────────────────────────────
    // Resultados del Sellado Digital y Timbrado SAT
    // ──────────────────────────────────────────────
    public string? SelloDigitalEmisor { get; private set; }
    public string? SelloDigitalSAT { get; private set; }
    public string? NoCertificadoEmisor { get; private set; }
    public string? NoCertificadoSAT { get; private set; }
    public string? CadenaOriginal { get; private set; }

    // ──────────────────────────────────────────────
    // Relaciones SAT (Para Notas de Crédito / Sustituciones)
    // ──────────────────────────────────────────────
    /// <summary>UUID del CFDI relacionado (ej. la factura que se cancela o abona).</summary>
    public string? RelatedUuid { get; private set; }
    /// <summary>Tipo de relación SAT (Catálogo c_TipoRelacion). Ej: "01" Nota de crédito.</summary>
    public string? RelationType { get; private set; }

    // ──────────────────────────────────────────────
    // Receptor (quien recibe la factura)
    // ──────────────────────────────────────────────
    public string ReceiverTaxId { get; private set; }   // RFC (XAXX010101000 para global)
    public string ReceiverName { get; private set; }
    public CfdiUsage CfdiUsage { get; private set; }

    // ──────────────────────────────────────────────
    // Montos
    // ──────────────────────────────────────────────
    public decimal Subtotal { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal Total { get; private set; }

    // ──────────────────────────────────────────────
    // Vínculos — Solo uno aplica según el tipo
    // ──────────────────────────────────────────────
    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }

    public Guid? SaleId { get; private set; }        // Factura de cliente
    public Sale? Sale { get; private set; }

    public Guid? ShiftId { get; private set; }       // Factura global
    public Shift? Shift { get; private set; }

    public Guid? ClientId { get; private set; }      // Solo para facturas de cliente
    public Client? Client { get; private set; }

    public Guid? ReturnId { get; private set; }      // Nota de crédito por devolución
    public Return? Return { get; private set; }

    // ──────────────────────────────────────────────
    // Desglose de Impuestos (IVA 16%, IEPS 8%, Exento...)
    // ──────────────────────────────────────────────
    public IReadOnlyCollection<TaxBreakdown> TaxBreakdowns => _taxBreakdowns.AsReadOnly();

#pragma warning disable CS8618
    private Invoice() { } // Para EF Core
#pragma warning restore CS8618

    // ──────────────────────────────────────────────
    // Factory: Factura a Cliente Individual
    // ──────────────────────────────────────────────
    public static Invoice CreateCustomerInvoice(
        Guid branchId,
        string series,
        string folio,
        Guid saleId,
        Guid? clientId,
        string receiverTaxId,
        string receiverName,
        CfdiUsage cfdiUsage,
        decimal subtotal,
        IEnumerable<TaxBreakdown> taxBreakdowns)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");
        if (string.IsNullOrWhiteSpace(series)) throw new DomainException("La serie es requerida.");
        if (string.IsNullOrWhiteSpace(folio)) throw new DomainException("El folio es requerido.");
        if (saleId == Guid.Empty) throw new DomainException("El ID de venta es requerido para una factura de cliente.");
        if (string.IsNullOrWhiteSpace(receiverTaxId)) throw new DomainException("El RFC del receptor es requerido.");
        if (string.IsNullOrWhiteSpace(receiverName)) throw new DomainException("El nombre del receptor es requerido.");
        if (subtotal < 0) throw new DomainException("El subtotal no puede ser negativo.");

        var breakdowns = taxBreakdowns?.ToList() ?? new List<TaxBreakdown>();

        var invoice = new Invoice
        {
            BranchId = branchId,
            Type = InvoiceType.Customer,
            Status = InvoiceStatus.Draft,
            Series = series.Trim(),
            Folio = folio.Trim(),
            SaleId = saleId,
            ClientId = clientId,
            ReceiverTaxId = receiverTaxId.Trim().ToUpperInvariant(),
            ReceiverName = receiverName.Trim(),
            CfdiUsage = cfdiUsage,
            Subtotal = subtotal,
            InvoiceDate = DateTime.UtcNow
        };

        invoice._taxBreakdowns.AddRange(breakdowns);
        invoice.TotalTax = breakdowns.Sum(b => b.TaxAmount);
        invoice.Total = subtotal + invoice.TotalTax;

        invoice.AddDomainEvent(new InvoiceCreatedEvent(invoice.Id, invoice.BranchId, invoice.Type, saleId, null));
        return invoice;
    }

    // ──────────────────────────────────────────────
    // Factory: Factura Global de Turno
    // ──────────────────────────────────────────────
    public static Invoice CreateGlobalInvoice(
        Guid branchId,
        string series,
        string folio,
        Guid shiftId,
        decimal subtotal,
        IEnumerable<TaxBreakdown> taxBreakdowns)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");
        if (string.IsNullOrWhiteSpace(series)) throw new DomainException("La serie es requerida.");
        if (string.IsNullOrWhiteSpace(folio)) throw new DomainException("El folio es requerido.");
        if (shiftId == Guid.Empty) throw new DomainException("El ID de turno es requerido para una factura global.");
        if (subtotal < 0) throw new DomainException("El subtotal no puede ser negativo.");

        var breakdowns = taxBreakdowns?.ToList() ?? new List<TaxBreakdown>();

        var invoice = new Invoice
        {
            BranchId = branchId,
            Type = InvoiceType.Global,
            Status = InvoiceStatus.Draft,
            Series = series.Trim(),
            Folio = folio.Trim(),
            ShiftId = shiftId,
            ReceiverTaxId = "XAXX010101000",   // RFC genérico SAT para público en general
            ReceiverName = "PUBLICO EN GENERAL",
            CfdiUsage = CfdiUsage.ToDefine,     // S01 - obligatorio en facturas globales
            Subtotal = subtotal,
            InvoiceDate = DateTime.UtcNow
        };

        invoice._taxBreakdowns.AddRange(breakdowns);
        invoice.TotalTax = breakdowns.Sum(b => b.TaxAmount);
        invoice.Total = subtotal + invoice.TotalTax;

        invoice.AddDomainEvent(new InvoiceCreatedEvent(invoice.Id, invoice.BranchId, invoice.Type, null, shiftId));
        return invoice;
    }

    // ──────────────────────────────────────────────
    // Factory: Nota de Crédito (Egreso)
    // ──────────────────────────────────────────────
    public static Invoice CreateCreditNote(
        Guid branchId,
        string series,
        string folio,
        Guid returnId,
        Guid clientId,
        string receiverTaxId,
        string receiverName,
        string relatedUuid,
        decimal subtotal,
        IEnumerable<TaxBreakdown> taxBreakdowns)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");
        if (string.IsNullOrWhiteSpace(series)) throw new DomainException("La serie es requerida.");
        if (string.IsNullOrWhiteSpace(folio)) throw new DomainException("El folio es requerido.");
        if (returnId == Guid.Empty) throw new DomainException("El ID de devolución es requerido para una nota de crédito.");
        if (clientId == Guid.Empty) throw new DomainException("El ID de cliente es requerido.");
        if (string.IsNullOrWhiteSpace(receiverTaxId)) throw new DomainException("El RFC del receptor es requerido.");
        if (string.IsNullOrWhiteSpace(receiverName)) throw new DomainException("El nombre del receptor es requerido.");
        if (string.IsNullOrWhiteSpace(relatedUuid)) throw new DomainException("El UUID relacionado es obligatorio para una nota de crédito.");
        if (subtotal < 0) throw new DomainException("El subtotal no puede ser negativo.");

        var breakdowns = taxBreakdowns?.ToList() ?? new List<TaxBreakdown>();

        var invoice = new Invoice
        {
            BranchId = branchId,
            Type = InvoiceType.CreditNote,
            Status = InvoiceStatus.Draft,
            Series = series.Trim(),
            Folio = folio.Trim(),
            ReturnId = returnId,
            ClientId = clientId,
            ReceiverTaxId = receiverTaxId.Trim().ToUpperInvariant(),
            ReceiverName = receiverName.Trim(),
            CfdiUsage = CfdiUsage.GeneralExpense, // G02 (Devoluciones) o similar. Usamos uno por defecto.
            RelatedUuid = relatedUuid.Trim().ToUpperInvariant(),
            RelationType = "01", // "01" - Nota de crédito de los documentos relacionados
            Subtotal = subtotal,
            InvoiceDate = DateTime.UtcNow
        };

        invoice._taxBreakdowns.AddRange(breakdowns);
        invoice.TotalTax = breakdowns.Sum(b => b.TaxAmount);
        invoice.Total = subtotal + invoice.TotalTax;

        invoice.AddDomainEvent(new InvoiceCreatedEvent(invoice.Id, invoice.BranchId, invoice.Type, null, null, returnId));
        return invoice;
    }


    // ──────────────────────────────────────────────
    // Comportamientos
    // ──────────────────────────────────────────────

    /// <summary>
    /// Registra el UUID devuelto por el PAC tras timbrar exitosamente.
    /// </summary>
    public void Stamp(string uuid)
    {
        Stamp(uuid, DateTime.UtcNow, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// Registra los datos fiscales detallados devueltos por el PAC tras timbrar exitosamente.
    /// </summary>
    public void Stamp(
        string uuid,
        DateTime stampedAt,
        string selloDigitalEmisor,
        string selloDigitalSAT,
        string noCertificadoEmisor,
        string noCertificadoSAT,
        string cadenaOriginal)
    {
        if (Status == InvoiceStatus.CancelledAtSat || Status == InvoiceStatus.VoidedInSystem)
            throw new DomainException("No se puede timbrar una factura cancelada.");
        if (Status == InvoiceStatus.Stamped)
            throw new DomainException("La factura ya ha sido timbrada.");
        if (string.IsNullOrWhiteSpace(uuid))
            throw new DomainException("El UUID es requerido para timbrar.");

        Uuid = uuid.Trim().ToUpperInvariant();
        Status = InvoiceStatus.Stamped;
        StampedAt = stampedAt;
        SelloDigitalEmisor = selloDigitalEmisor;
        SelloDigitalSAT = selloDigitalSAT;
        NoCertificadoEmisor = noCertificadoEmisor;
        NoCertificadoSAT = noCertificadoSAT;
        CadenaOriginal = cadenaOriginal;

        AddDomainEvent(new InvoiceStampedEvent(Id, Uuid));
    }

    /// <summary>
    /// Anula la factura solo en el sistema interno.
    /// Útil cuando nunca fue timbrada o el PAC está caído y se necesita
    /// invalidarla localmente sin efecto fiscal ante el SAT.
    /// </summary>
    public void VoidInSystem(string reason)
    {
        if (Status == InvoiceStatus.CancelledAtSat)
            throw new DomainException("No se puede anular en sistema una factura ya cancelada ante el SAT.");
        if (Status == InvoiceStatus.VoidedInSystem)
            throw new DomainException("La factura ya se encuentra anulada en sistema.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Se requiere un motivo para anular la factura.");

        Status = InvoiceStatus.VoidedInSystem;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();

        AddDomainEvent(new InvoiceVoidedInSystemEvent(Id, CancellationReason));
    }

    /// <summary>
    /// Cancela la factura formalmente ante el SAT.
    /// Solo aplica a facturas timbradas. Requiere el motivo oficial del SAT.
    /// Si el motivo es ErrorWithRelation (01), es obligatorio proporcionar el UUID sustituto.
    /// </summary>
    public void CancelAtSat(SatCancellationMotif motif, string reason, string? substituteUuid = null)
    {
        if (Status == InvoiceStatus.CancelledAtSat)
            throw new DomainException("La factura ya se encuentra cancelada ante el SAT.");
        if (Status != InvoiceStatus.Stamped)
            throw new DomainException("Solo se pueden cancelar ante el SAT facturas que hayan sido timbradas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Se requiere una razón para cancelar la factura.");
        if (motif == Enums.SatCancellationMotif.ErrorWithRelation && string.IsNullOrWhiteSpace(substituteUuid))
            throw new DomainException("El motivo '01 - Error con relación' requiere el UUID del CFDI sustituto.");

        Status = InvoiceStatus.CancelledAtSat;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason.Trim();
        SatCancellationMotif = motif;
        SubstituteUuid = substituteUuid?.Trim().ToUpperInvariant();

        AddDomainEvent(new InvoiceCancelledAtSatEvent(Id, Uuid!, motif, SubstituteUuid));
    }
}