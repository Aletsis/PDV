namespace PDV.Domain.ValueObjects;

/// <summary>
/// Value Object para representar dinero/moneda
/// </summary>
public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "MXN";

    private Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("El monto no puede ser negativo", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("La moneda no puede estar vacía", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public static Money Create(decimal amount, string currency = "MXN")
    {
        return new Money(amount, currency);
    }

    public static Money Zero(string currency = "MXN") => Create(0, currency);

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("No se pueden sumar montos de diferentes monedas");

        return Create(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("No se pueden restar montos de diferentes monedas");

        return Create(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return Create(money.Amount * multiplier, money.Currency);
    }

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException("No se puede dividir por cero");

        return Create(money.Amount / divisor, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("No se pueden comparar montos de diferentes monedas");

        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("No se pueden comparar montos de diferentes monedas");

        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right) => !(left < right);
    public static bool operator <=(Money left, Money right) => !(left > right);

    public static implicit operator decimal(Money money) => money.Amount;
}
