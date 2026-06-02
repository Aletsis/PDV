namespace PDV.HardwareAgent.Contracts.Requests;

public record PrintTextRequest(
    string Ip, 
    int Port, 
    string Text, 
    int? EncodingCodePage = null);
