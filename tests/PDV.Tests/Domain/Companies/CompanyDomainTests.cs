using System;
using PDV.Domain.Entities;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.Companies;

public class CompanyDomainTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var address = Address.Create("Av. Vallarta 100", "Guadalajara", "Jalisco", "44100", "México");

        // Act
        var company = new Company(
            name: "Comercializadora POS",
            rfc: "CPOS123456XX9",
            fiscalAddress: address,
            phone: "3312345678",
            email: "contacto@comercializadorapos.com"
        );

        // Assert
        Assert.Equal("Comercializadora POS", company.Name);
        Assert.Equal("CPOS123456XX9", company.RFC);
        Assert.Equal(address, company.FiscalAddress);
        Assert.Equal("3312345678", company.Phone);
        Assert.Equal("contacto@comercializadorapos.com", company.Email);
        Assert.True(company.IsActive);
    }

    [Theory]
    [InlineData("", "CPOS123456XX9", "El nombre de la empresa es requerido.")]
    [InlineData("Comercializadora POS", "", "El RFC de la empresa es requerido.")]
    public void Constructor_WithInvalidParameters_ThrowsDomainException(string name, string rfc, string expectedMessage)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new Company(
            name: name,
            rfc: rfc,
            fiscalAddress: null,
            phone: "3312345678"
        ));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void Update_WithValidParameters_UpdatesCorrectly()
    {
        // Arrange
        var company = new Company("Original Name", "CPOS123456XX9", null, "3312345678");
        var newAddress = Address.Create("Av. Juárez 500", "Guadalajara", "Jalisco", "44100", "México");

        // Act
        company.Update("New Name", "CPOS123456XX0", newAddress, "3387654321", "new@email.com");

        // Assert
        Assert.Equal("New Name", company.Name);
        Assert.Equal("CPOS123456XX0", company.RFC);
        Assert.Equal(newAddress, company.FiscalAddress);
        Assert.Equal("3387654321", company.Phone);
        Assert.Equal("new@email.com", company.Email);
    }

    [Fact]
    public void ActivateAndDeactivate_StateTransitionsCorrectly()
    {
        // Arrange
        var company = new Company("Test Company", "CPOS123456XX9", null, "3312345678");
        Assert.True(company.IsActive);

        // Act - Deactivate
        company.Deactivate();
        // Assert
        Assert.False(company.IsActive);

        // Act - Activate
        company.Activate();
        // Assert
        Assert.True(company.IsActive);
    }
}
