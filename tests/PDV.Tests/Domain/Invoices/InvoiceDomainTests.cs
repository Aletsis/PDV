using System;
using System.Collections.Generic;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.Invoices;

public class InvoiceDomainTests
{
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _saleId = Guid.NewGuid();
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid _shiftId = Guid.NewGuid();
    private readonly Guid _returnId = Guid.NewGuid();

    [Fact]
    public void CreateCustomerInvoice_WithValidParameters_InitializesCorrectlyAndRaisesEvent()
    {
        // Arrange
        var taxBreakdowns = new List<TaxBreakdown>
        {
            new TaxBreakdown(16m, 100m, 16m, false)
        };

        // Act
        var invoice = Invoice.CreateCustomerInvoice(
            branchId: _branchId,
            series: "F",
            folio: "101",
            saleId: _saleId,
            clientId: _clientId,
            receiverTaxId: "XAXX010101000",
            receiverName: "Cliente Publico",
            cfdiUsage: CfdiUsage.GeneralExpense,
            subtotal: 100m,
            taxBreakdowns: taxBreakdowns
        );

        // Assert
        Assert.Equal(_branchId, invoice.BranchId);
        Assert.Equal(InvoiceType.Customer, invoice.Type);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal("F101", invoice.InvoiceNumber);
        Assert.Equal("XAXX010101000", invoice.ReceiverTaxId);
        Assert.Equal("Cliente Publico", invoice.ReceiverName);
        Assert.Equal(100m, invoice.Subtotal);
        Assert.Equal(16m, invoice.TotalTax);
        Assert.Equal(116m, invoice.Total);
        Assert.False(invoice.IsGlobal);

        var createdEvent = invoice.DomainEvents.OfType<InvoiceCreatedEvent>().FirstOrDefault();
        Assert.NotNull(createdEvent);
        Assert.Equal(invoice.Id, createdEvent!.InvoiceId);
        Assert.Equal(_saleId, createdEvent.SaleId);
        Assert.Null(createdEvent.ShiftId);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "F", "101", "00000000-0000-0000-0000-000000000002", "XAXX010101000", "Publico", 100, "El ID de sucursal es requerido.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "", "101", "00000000-0000-0000-0000-000000000002", "XAXX010101000", "Publico", 100, "La serie es requerida.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "F", "", "00000000-0000-0000-0000-000000000002", "XAXX010101000", "Publico", 100, "El folio es requerido.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "F", "101", "00000000-0000-0000-0000-000000000000", "XAXX010101000", "Publico", 100, "El ID de venta es requerido para una factura de cliente.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "F", "101", "00000000-0000-0000-0000-000000000002", "", "Publico", 100, "El RFC del receptor es requerido.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "F", "101", "00000000-0000-0000-0000-000000000002", "XAXX010101000", " ", 100, "El nombre del receptor es requerido.")]
    [InlineData("00000000-0000-0000-0000-000000000001", "F", "101", "00000000-0000-0000-0000-000000000002", "XAXX010101000", "Publico", -50, "El subtotal no puede ser negativo.")]
    public void CreateCustomerInvoice_WithInvalidParameters_ThrowsDomainException(
        string branchIdString, string series, string folio, string saleIdString, string receiverTaxId, string receiverName, decimal subtotal, string expectedMsg)
    {
        // Arrange
        var branchId = Guid.Parse(branchIdString);
        var saleId = Guid.Parse(saleIdString);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => Invoice.CreateCustomerInvoice(
            branchId, series, folio, saleId, Guid.NewGuid(), receiverTaxId, receiverName, CfdiUsage.GeneralExpense, subtotal, null!));
        Assert.Equal(expectedMsg, exception.Message);
    }

    [Fact]
    public void CreateGlobalInvoice_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var invoice = Invoice.CreateGlobalInvoice(
            branchId: _branchId,
            series: "G",
            folio: "501",
            shiftId: _shiftId,
            subtotal: 1000m,
            taxBreakdowns: null!
        );

        // Assert
        Assert.True(invoice.IsGlobal);
        Assert.Equal("XAXX010101000", invoice.ReceiverTaxId);
        Assert.Equal("PUBLICO EN GENERAL", invoice.ReceiverName);
        Assert.Equal(CfdiUsage.ToDefine, invoice.CfdiUsage);
        Assert.Equal(1000m, invoice.Total);
    }

    [Fact]
    public void CreateCreditNote_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var uuid = Guid.NewGuid().ToString();

        // Act
        var invoice = Invoice.CreateCreditNote(
            branchId: _branchId,
            series: "NC",
            folio: "201",
            returnId: _returnId,
            clientId: _clientId,
            receiverTaxId: "XAXX010101000",
            receiverName: "Cliente",
            relatedUuid: uuid,
            subtotal: 500m,
            taxBreakdowns: null!
        );

        // Assert
        Assert.Equal(InvoiceType.CreditNote, invoice.Type);
        Assert.Equal(uuid.ToUpperInvariant(), invoice.RelatedUuid);
        Assert.Equal("01", invoice.RelationType);
        Assert.Equal(500m, invoice.Total);
    }

    [Fact]
    public void Stamp_WithValidUuid_ChangesStatusAndRaisesEvent()
    {
        // Arrange
        var invoice = Invoice.CreateGlobalInvoice(_branchId, "G", "1", _shiftId, 100m, null!);
        var uuid = Guid.NewGuid().ToString();

        // Act
        invoice.Stamp(uuid);

        // Assert
        Assert.Equal(InvoiceStatus.Stamped, invoice.Status);
        Assert.Equal(uuid.ToUpperInvariant(), invoice.Uuid);
        Assert.NotNull(invoice.StampedAt);

        var stampedEvent = invoice.DomainEvents.OfType<InvoiceStampedEvent>().FirstOrDefault();
        Assert.NotNull(stampedEvent);
        Assert.Equal(invoice.Id, stampedEvent!.InvoiceId);
        Assert.Equal(uuid.ToUpperInvariant(), stampedEvent.Uuid);
    }

    [Fact]
    public void Stamp_OnAlreadyStampedOrCancelled_ThrowsDomainException()
    {
        // Arrange
        var invoice = Invoice.CreateGlobalInvoice(_branchId, "G", "1", _shiftId, 100m, null!);
        invoice.Stamp(Guid.NewGuid().ToString());

        // Act & Assert - Timbrar ya timbrada
        var exception1 = Assert.Throws<DomainException>(() => invoice.Stamp(Guid.NewGuid().ToString()));
        Assert.Equal("La factura ya ha sido timbrada.", exception1.Message);

        // Cancelar e intentar timbrar
        var invoice2 = Invoice.CreateGlobalInvoice(_branchId, "G", "1", _shiftId, 100m, null!);
        invoice2.VoidInSystem("Anulada localmente");
        var exception2 = Assert.Throws<DomainException>(() => invoice2.Stamp(Guid.NewGuid().ToString()));
        Assert.Equal("No se puede timbrar una factura cancelada.", exception2.Message);
    }

    [Fact]
    public void VoidInSystem_ChangesStatusAndRaisesEvent()
    {
        // Arrange
        var invoice = Invoice.CreateGlobalInvoice(_branchId, "G", "1", _shiftId, 100m, null!);

        // Act
        invoice.VoidInSystem("Error en importes");

        // Assert
        Assert.Equal(InvoiceStatus.VoidedInSystem, invoice.Status);
        Assert.Equal("Error en importes", invoice.CancellationReason);

        var voidedEvent = invoice.DomainEvents.OfType<InvoiceVoidedInSystemEvent>().FirstOrDefault();
        Assert.NotNull(voidedEvent);
        Assert.Equal(invoice.Id, voidedEvent!.InvoiceId);
        Assert.Equal("Error en importes", voidedEvent.Reason);
    }

    [Fact]
    public void CancelAtSat_WithRelationButNoSubstituteUuid_ThrowsDomainException()
    {
        // Arrange
        var invoice = Invoice.CreateGlobalInvoice(_branchId, "G", "1", _shiftId, 100m, null!);
        invoice.Stamp(Guid.NewGuid().ToString());

        // Act & Assert - Motivo 01 requiere UUID sustituto
        var exception = Assert.Throws<DomainException>(() => invoice.CancelAtSat(SatCancellationMotif.ErrorWithRelation, "Cancelacion por error", null));
        Assert.Equal("El motivo '01 - Error con relación' requiere el UUID del CFDI sustituto.", exception.Message);
    }

    [Fact]
    public void CancelAtSat_WithValidParameters_CancelsAndRaisesEvent()
    {
        // Arrange
        var invoice = Invoice.CreateGlobalInvoice(_branchId, "G", "1", _shiftId, 100m, null!);
        var uuid = Guid.NewGuid().ToString();
        var subUuid = Guid.NewGuid().ToString();
        invoice.Stamp(uuid);

        // Act
        invoice.CancelAtSat(SatCancellationMotif.ErrorWithRelation, "Sustitucion", subUuid);

        // Assert
        Assert.Equal(InvoiceStatus.CancelledAtSat, invoice.Status);
        Assert.Equal("Sustitucion", invoice.CancellationReason);
        Assert.Equal(SatCancellationMotif.ErrorWithRelation, invoice.SatCancellationMotif);
        Assert.Equal(subUuid.ToUpperInvariant(), invoice.SubstituteUuid);

        var cancelledEvent = invoice.DomainEvents.OfType<InvoiceCancelledAtSatEvent>().FirstOrDefault();
        Assert.NotNull(cancelledEvent);
        Assert.Equal(invoice.Id, cancelledEvent!.InvoiceId);
        Assert.Equal(uuid.ToUpperInvariant(), cancelledEvent.Uuid);
        Assert.Equal(SatCancellationMotif.ErrorWithRelation, cancelledEvent.Motif);
        Assert.Equal(subUuid.ToUpperInvariant(), cancelledEvent.SubstituteUuid);
    }
}
