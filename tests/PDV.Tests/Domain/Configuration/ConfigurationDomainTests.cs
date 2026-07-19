using System;
using System.Linq;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.Configuration;

public class ConfigurationDomainTests
{
    [Fact]
    public void SystemConfiguration_Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var config = new SystemConfiguration(
            companyName: "Mi Empresa SA de CV",
            taxId: "AAA010101AAA", // 12 chars (persona moral)
            fiscalRegime: "601",
            currency: "MXN",
            phone: "5551234567",
            email: "contacto@empresa.com"
        );

        // Assert
        Assert.Equal("Mi Empresa SA de CV", config.CompanyName);
        Assert.Equal("AAA010101AAA", config.TaxId);
        Assert.Equal("601", config.FiscalRegime);
        Assert.Equal("MXN", config.Currency);
        Assert.Equal("5551234567", config.Phone);
        Assert.Equal("contacto@empresa.com", config.Email);
        Assert.Equal(48, config.TicketWidth);
        Assert.True(config.PrintLogoOnTicket);
        Assert.True(config.AutoPrintTicket);

        Assert.Single(config.DomainEvents.OfType<SystemConfigurationUpdatedEvent>());
    }

    [Theory]
    [InlineData("AAA010101AA", "El RFC 'AAA010101AA' no tiene una longitud válida")] // 11 chars (corto)
    [InlineData("AAA010101AAAAA", "El RFC 'AAA010101AAAAA' no tiene una longitud válida")] // 14 chars (largo)
    [InlineData("", "El RFC (TaxId) es requerido.")]
    public void SystemConfiguration_WithInvalidTaxId_ThrowsDomainException(string taxId, string expectedMessage)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new SystemConfiguration("Empresa", taxId, "601"));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void SystemConfiguration_UpdateTicketSettings_WithInvalidValues_ThrowsDomainException()
    {
        // Arrange
        var config = new SystemConfiguration("Empresa", "AAA010101AAA", "601");

        // Act & Assert - Ancho muy pequeño
        var ex1 = Assert.Throws<DomainException>(() => config.UpdateTicketSettings(30, true, true));
        Assert.Equal("El ancho del ticket debe estar entre 32 y 80 caracteres.", ex1.Message);

        // Act & Assert - Copias invalidas
        var ex2 = Assert.Throws<DomainException>(() => config.UpdateTicketSettings(48, true, true, ticketCopies: 6));
        Assert.Equal("El número de copias debe estar entre 1 y 5.", ex2.Message);
    }

    [Fact]
    public void SystemConfiguration_UpdateInvoiceSettings_And_CsdValidation_Works()
    {
        // Arrange
        var config = new SystemConfiguration("Empresa", "AAA010101AAA", "601");
        Assert.False(config.IsCsdValid());

        var expires = DateTime.UtcNow.AddYears(2);

        // Act
        config.UpdateInvoiceSettings("CSD-123", expires, "https://pac.com/api", "api-user");

        // Assert
        Assert.True(config.IsCsdValid());
        Assert.Equal("CSD-123", config.CsdSerialNumber);
        Assert.Equal(expires, config.CsdExpiresAt);
        Assert.Single(config.DomainEvents.OfType<InvoiceSettingsUpdatedEvent>());
    }

    [Fact]
    public void TicketSequence_GetNextTicketNumber_IncrementsValueAndRaisesEvent()
    {
        // Arrange
        var cashRegisterId = Guid.NewGuid();
        var sequence = new TicketSequence(cashRegisterId, TicketSequenceType.Sale, "C1");

        Assert.Equal(0, sequence.LastTicketNumber);
        Assert.Equal("C1", sequence.Series);
        Assert.False(sequence.ResetOnNewShift);

        // Act
        var next = sequence.GetNextTicketNumber();

        // Assert
        Assert.Equal(1, next);
        Assert.Equal(1, sequence.LastTicketNumber);
        Assert.Single(sequence.DomainEvents.OfType<TicketIssuedEvent>());
        Assert.Equal("C1000001", sequence.FormatTicket(next));
    }

    [Fact]
    public void TicketSequence_ResetForNewShift_ThrowsIfNotResettable()
    {
        // Arrange - Sale sequences are not resettable by shift
        var sequence = new TicketSequence(Guid.NewGuid(), TicketSequenceType.Sale);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => sequence.ResetForNewShift());
        Assert.Contains("no está configurada para reiniciarse por turno", exception.Message);
    }

    [Fact]
    public void TicketSequence_ResetForNewShift_ResetsCorrectlyIfResettable()
    {
        // Arrange - CashCollection sequences are resettable by shift
        var sequence = new TicketSequence(Guid.NewGuid(), TicketSequenceType.CashCollection);
        Assert.True(sequence.ResetOnNewShift);

        sequence.GetNextTicketNumber();
        Assert.Equal(1, sequence.LastTicketNumber);

        // Act
        sequence.ResetForNewShift();

        // Assert
        Assert.Equal(0, sequence.LastTicketNumber);
    }

    [Fact]
    public void TicketSequence_ResetTo_WithNegativeNumber_ThrowsDomainException()
    {
        // Arrange
        var sequence = new TicketSequence(Guid.NewGuid(), TicketSequenceType.Sale);

        // Act & Assert
        Assert.Throws<DomainException>(() => sequence.ResetTo(-10));
    }
}
