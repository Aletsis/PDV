namespace PDV.HardwareAgent.Contracts.Requests;

public class CancelPaymentRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Protocol { get; set; } = "Mock";
}
