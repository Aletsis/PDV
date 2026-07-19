using System;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;
using Xunit;

namespace PDV.Tests.Domain.ValueObjects;

public class ValueObjectsDomainTests
{
    [Fact]
    public void Money_Create_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var money = Money.Create(100.50m, "MXN");

        // Assert
        Assert.Equal(100.50m, money.Amount);
        Assert.Equal("MXN", money.Currency);
        
        decimal decVal = money; // implicit conversion
        Assert.Equal(100.50m, decVal);
    }

    [Fact]
    public void Money_Create_WithNegativeAmount_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Create(-10m));
    }

    [Fact]
    public void Money_Create_WithNullOrEmptyCurrency_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Create(10m, ""));
    }

    [Fact]
    public void Money_ArithmeticOperators_CalculateCorrectly()
    {
        // Arrange
        var m1 = Money.Create(100m, "MXN");
        var m2 = Money.Create(50m, "MXN");

        // Act & Assert
        Assert.Equal(150m, (m1 + m2).Amount);
        Assert.Equal(50m, (m1 - m2).Amount);
        Assert.Equal(200m, (m1 * 2m).Amount);
        Assert.Equal(25m, (m2 / 2m).Amount);
    }

    [Fact]
    public void Money_ArithmeticWithDifferentCurrencies_ThrowsInvalidOperationException()
    {
        // Arrange
        var mxn = Money.Create(100m, "MXN");
        var usd = Money.Create(5m, "USD");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => mxn + usd);
        Assert.Throws<InvalidOperationException>(() => mxn - usd);
        Assert.Throws<InvalidOperationException>(() => mxn > usd);
    }

    [Fact]
    public void Money_ComparisonOperators_WorksCorrectly()
    {
        // Arrange
        var m1 = Money.Create(100m, "MXN");
        var m2 = Money.Create(50m, "MXN");

        // Act & Assert
        Assert.True(m1 > m2);
        Assert.True(m2 < m1);
        Assert.True(m1 >= m2);
        Assert.True(m2 <= m1);
    }

    [Fact]
    public void Address_Create_InitializesProperties()
    {
        // Act
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "Mexico");

        // Assert
        Assert.Equal("Calle Falsa 123", address.Street);
        Assert.Equal("Centro", address.City);
        Assert.Equal("CDMX", address.State);
        Assert.Equal("06000", address.ZipCode);
        Assert.Equal("Mexico", address.Country);
    }

    [Fact]
    public void DateRange_Create_WithValidDates_InitializesCorrectly()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddDays(5);

        // Act
        var range = DateRange.Create(start, end);

        // Assert
        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
        Assert.True(range.Includes(start.AddDays(2)));
        Assert.False(range.Includes(start.AddDays(-1)));
    }

    [Fact]
    public void DateRange_Create_WithStartAfterEnd_ThrowsArgumentException()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddDays(-2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DateRange.Create(start, end));
    }

    [Fact]
    public void CashDenomination_Create_WithValidParams_CalculatesTotal()
    {
        // Act
        var denomination = new CashDenomination(DenominationType.Bill_500, 3);

        // Assert
        Assert.Equal(DenominationType.Bill_500, denomination.Type);
        Assert.Equal(3, denomination.Quantity);
        Assert.Equal(1500m, denomination.TotalValue);
    }

    [Fact]
    public void CashDenomination_Create_WithNegativeQuantity_ThrowsDomainException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new CashDenomination(DenominationType.Bill_500, -1));
        Assert.Equal("La cantidad de billetes/monedas no puede ser negativa.", exception.Message);
    }
}
