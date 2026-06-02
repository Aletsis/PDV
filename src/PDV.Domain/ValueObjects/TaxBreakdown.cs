namespace PDV.Domain.ValueObjects;

public record TaxBreakdown(decimal Rate, decimal BaseAmount, decimal TaxAmount, bool IsExempt);

