namespace PDV.HardwareAgent.Contracts.Requests;

public class ScaleRequest
{
    public string Port { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 9600;
    public string Protocol { get; set; } = "Mock";
}
