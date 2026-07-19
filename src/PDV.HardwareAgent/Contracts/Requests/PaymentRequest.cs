namespace PDV.HardwareAgent.Contracts.Requests;

public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "Sale";
    public string Port { get; set; } = string.Empty;
    public string Protocol { get; set; } = "Mock";
}
