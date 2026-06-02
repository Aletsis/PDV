namespace PDV.HardwareAgent.Contracts.Requests;

public record PrintQrRequest(
    string Ip, 
    int Port, 
    string Data, 
    int ModuleSize = 4, 
    int ErrorLevel = 48);
