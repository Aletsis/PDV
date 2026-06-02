using PDV.Domain.Enums;

namespace PDV.Domain.ValueObjects;

public record PaymentMethodBreakdown(PaymentMethodType PaymentMethod, decimal Amount);
