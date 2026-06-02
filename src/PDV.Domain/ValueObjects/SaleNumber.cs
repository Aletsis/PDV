namespace PDV.Domain.ValueObjects;

/// <summary>
/// Value Object para número de venta/ticket
/// </summary>
public record SaleNumber
{
    public string Value { get; init; }

    private SaleNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El número de venta no puede estar vacío", nameof(value));

        if (value.Length > 50)
            throw new ArgumentException("El número de venta no puede exceder 50 caracteres", nameof(value));

        Value = value;
    }

    public static SaleNumber Create(string value)
    {
        return new SaleNumber(value);
    }

    public static SaleNumber Generate()
    {
        var timestamp = DateTime.UtcNow.Ticks;
        return new SaleNumber($"T-{timestamp}");
    }

    public static implicit operator string(SaleNumber saleNumber) => saleNumber.Value;
    public static implicit operator SaleNumber(string value) => Create(value);
}
