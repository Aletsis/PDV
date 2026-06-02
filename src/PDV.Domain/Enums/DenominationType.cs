namespace PDV.Domain.Enums;

public enum DenominationType
{
    Bill_1000,
    Bill_500,
    Bill_200,
    Bill_100,
    Bill_50,
    Bill_20,
    Coin_20,
    Coin_10,
    Coin_5,
    Coin_2,
    Coin_1,
    Coin_050,
    Coin_020,
    Coin_010
}

public static class DenominationTypeExtensions
{
    public static decimal GetValue(this DenominationType denomination)
    {
        return denomination switch
        {
            DenominationType.Bill_1000 => 1000m,
            DenominationType.Bill_500 => 500m,
            DenominationType.Bill_200 => 200m,
            DenominationType.Bill_100 => 100m,
            DenominationType.Bill_50 => 50m,
            DenominationType.Bill_20 => 20m,
            DenominationType.Coin_20 => 20m,
            DenominationType.Coin_10 => 10m,
            DenominationType.Coin_5 => 5m,
            DenominationType.Coin_2 => 2m,
            DenominationType.Coin_1 => 1m,
            DenominationType.Coin_050 => 0.50m,
            DenominationType.Coin_020 => 0.20m,
            DenominationType.Coin_010 => 0.10m,
            _ => 0m
        };
    }
}
