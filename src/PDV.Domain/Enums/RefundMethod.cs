namespace PDV.Domain.Enums;

public enum RefundMethod
{
    /// <summary>Reembolso en efectivo al cliente.</summary>
    Cash = 1,
    /// <summary>Reembolso a la misma tarjeta con la que se pagó.</summary>
    Card = 2,
    /// <summary>Crédito en tienda (nota de crédito o saldo a favor).</summary>
    StoreCredit = 3
}
