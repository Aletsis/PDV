using System;
using System.Collections.Generic;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.Shifts;

public class ShiftDomainTests
{
    [Fact]
    public void Constructor_WithValidParameters_OpensCorrectlyAndRaisesEvent()
    {
        // Arrange
        var cashRegisterId = Guid.NewGuid();
        var userId = "cajero-01";
        var initialCash = 500m;

        // Act
        var shift = new Shift(cashRegisterId, userId, initialCash);

        // Assert
        Assert.Equal(cashRegisterId, shift.CashRegisterId);
        Assert.Equal(userId, shift.UserId);
        Assert.Equal(initialCash, shift.InitialCash);
        Assert.Equal(ShiftStatus.Open, shift.Status);
        Assert.False(shift.IsConsolidated);

        var openedEvent = shift.DomainEvents.OfType<ShiftOpenedEvent>().FirstOrDefault();
        Assert.NotNull(openedEvent);
        Assert.Equal(shift.Id, openedEvent!.ShiftId);
        Assert.Equal(initialCash, openedEvent.InitialCash);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "cajero-01", 100, "El ID de caja es inválido.")]
    [InlineData("a123bc45-1234-5678-abcd-123456789abc", "", 100, "El ID del usuario es requerido para abrir el turno.")]
    [InlineData("a123bc45-1234-5678-abcd-123456789abc", "cajero-01", -50, "El fondo inicial de caja no puede ser negativo.")]
    public void Constructor_WithInvalidParameters_ThrowsDomainException(string registerIdString, string userId, decimal initialCash, string expectedMessage)
    {
        // Arrange
        var cashRegisterId = Guid.Parse(registerIdString);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new Shift(cashRegisterId, userId, initialCash));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void Close_CalculatesExpectedSystemCashCorrectlyAndRaisesEvent()
    {
        // Arrange
        var shift = new Shift(Guid.NewGuid(), "cajero-01", 500m); // InitialCash = 500
        var endTime = DateTime.UtcNow.AddHours(8);

        var paymentBreakdowns = new List<PaymentMethodBreakdown>
        {
            new PaymentMethodBreakdown(PaymentMethodType.Cash, 1200m),
            new PaymentMethodBreakdown(PaymentMethodType.CreditCard, 300m)
        };

        var salesTaxes = new List<TaxBreakdown>
        {
            new TaxBreakdown(16m, 1293.10m, 206.90m, false)
        };

        var returnsTaxes = new List<TaxBreakdown>
        {
            new TaxBreakdown(16m, 86.20m, 13.80m, false)
        };

        // Formula: ExpectedCash = InitialCash + totalCashSales + totalInflows - totalCashReturns - totalOutflows
        // 500 + 1200 (sales in cash) + 200 (inflows) - 100 (cash returns) - 50 (outflows) = 1750 expected cash
        // Act
        shift.Close(
            endTime: endTime,
            totalCashSales: 1200m,
            totalCashReturns: 100m,
            totalInflows: 200m,
            totalOutflows: 50m,
            paymentMethodTotals: paymentBreakdowns,
            salesTaxTotals: salesTaxes,
            returnsTaxTotals: returnsTaxes
        );

        // Assert
        Assert.Equal(ShiftStatus.Closed, shift.Status);
        Assert.Equal(1750m, shift.SystemExpectedCash);
        Assert.Equal(100m, shift.TotalCashReturns);
        Assert.Equal(endTime, shift.EndTime);
        Assert.Equal(2, shift.PaymentMethodTotals.Count);
        Assert.Single(shift.SalesTaxTotals);
        Assert.Single(shift.ReturnsTaxTotals);

        var closedEvent = shift.DomainEvents.OfType<ShiftClosedEvent>().FirstOrDefault();
        Assert.NotNull(closedEvent);
        Assert.Equal(shift.Id, closedEvent!.ShiftId);
        Assert.Equal(1750m, closedEvent.SystemExpectedCash);
    }

    [Fact]
    public void Close_AlreadyClosedShift_ThrowsDomainException()
    {
        // Arrange
        var shift = new Shift(Guid.NewGuid(), "cajero-01", 500m);
        shift.Close(DateTime.UtcNow, 0, 0, 0, 0, null!, null!, null!);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => shift.Close(DateTime.UtcNow, 0, 0, 0, 0, null!, null!, null!));
        Assert.Equal("El turno ya se encuentra cerrado.", exception.Message);
    }

    [Fact]
    public void Close_EndTimePriorToStartTime_ThrowsDomainException()
    {
        // Arrange
        var shift = new Shift(Guid.NewGuid(), "cajero-01", 500m);
        var invalidEndTime = shift.StartTime.AddMinutes(-5);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => shift.Close(invalidEndTime, 0, 0, 0, 0, null!, null!, null!));
        Assert.Equal("La fecha-hora de cierre no puede ser anterior a la de apertura.", exception.Message);
    }

    [Fact]
    public void RequestGlobalInvoice_OnlyWhenClosed_TransitionsStateAndRaisesEvent()
    {
        // Arrange
        var shift = new Shift(Guid.NewGuid(), "cajero-01", 500m);

        // Act & Assert - abierto
        var exception = Assert.Throws<DomainException>(() => shift.RequestGlobalInvoice());
        Assert.Equal("Solo se puede solicitar la factura global de un turno cerrado.", exception.Message);

        // Cerrar
        shift.Close(DateTime.UtcNow, 100m, 0, 0, 0, null!, null!, null!);

        // Act - Cerrado
        shift.RequestGlobalInvoice();

        // Assert
        Assert.True(shift.IsGlobalInvoiceRequested);
        var requestedEvent = shift.DomainEvents.OfType<ShiftGlobalInvoiceRequestedEvent>().FirstOrDefault();
        Assert.NotNull(requestedEvent);
    }

    [Fact]
    public void RegisterCreditNote_OnlyIfGlobalInvoiced_SavesAndRaisesEvent()
    {
        // Arrange
        var shift = new Shift(Guid.NewGuid(), "cajero-01", 500m);
        shift.Close(DateTime.UtcNow, 1000m, 0, 0, 0, null!, null!, null!);

        // Act & Assert - Cerrado pero no facturado globalmente
        var exception = Assert.Throws<DomainException>(() => shift.RegisterCreditNote("CN-999", 150m, "Devolución de mercancía"));
        Assert.Equal("No se pueden registrar notas de crédito a un turno que no ha sido facturado globalmente.", exception.Message);

        // Facturar
        shift.MarkAsGlobalInvoiced("INV-GLOBAL-001");

        // Act - Facturado globalmente
        shift.RegisterCreditNote("CN-999", 150m, "Devolución de mercancía");

        // Assert
        Assert.Single(shift.CreditNotes);
        var creditNote = shift.CreditNotes.First();
        Assert.Equal("CN-999", creditNote.CreditNoteId);
        Assert.Equal(150m, creditNote.Amount);
        Assert.Equal("Devolución de mercancía", creditNote.Reason);

        var creditNoteEvent = shift.DomainEvents.OfType<ShiftCreditNoteRegisteredEvent>().FirstOrDefault();
        Assert.NotNull(creditNoteEvent);
        Assert.Equal(shift.Id, creditNoteEvent!.ShiftId);
        Assert.Equal(150m, creditNoteEvent.Amount);
    }
}
