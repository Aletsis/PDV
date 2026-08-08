namespace PDV.Domain.Enums;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Routed = 2,
    EnRoute = 3,
    Delivered = 4,
    Cancelled = 5,
    Returned = 6
}
