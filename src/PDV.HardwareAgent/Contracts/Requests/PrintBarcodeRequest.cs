namespace PDV.HardwareAgent.Contracts.Requests;

public record PrintBarcodeRequest(
    string Ip, 
    int Port, 
    string Data, 
    int BarcodeType = 73, 
    int Height = 100);
