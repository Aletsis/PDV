namespace PDV.Domain.Enums;

public enum OutboxState
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3
}
