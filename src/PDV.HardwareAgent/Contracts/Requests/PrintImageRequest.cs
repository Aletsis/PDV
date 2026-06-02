namespace PDV.HardwareAgent.Contracts.Requests;

public record PrintImageRequest(
    string Ip, 
    int Port, 
    string ImageBase64, 
    int MaxWidth = 384);
