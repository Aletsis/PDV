namespace PDV.Domain.ValueObjects;

/// <summary>
/// Value Object para método de pago
/// </summary>
public record PaymentMethod
{
    public string Value { get; init; }

    private static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cash",
        "Card",
        "Credit",
        "Debit",
        "Transfer",
        "Check"
    };

    private PaymentMethod(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El método de pago no puede estar vacío", nameof(value));

        if (!ValidMethods.Contains(value))
            throw new ArgumentException($"Método de pago inválido: {value}", nameof(value));

        Value = value;
    }

    public static PaymentMethod Create(string value)
    {
        return new PaymentMethod(value);
    }

    public static PaymentMethod Cash() => Create("Cash");
    public static PaymentMethod Card() => Create("Card");

    public static implicit operator string(PaymentMethod paymentMethod) => paymentMethod.Value;
    public static implicit operator PaymentMethod(string value) => Create(value);
}
