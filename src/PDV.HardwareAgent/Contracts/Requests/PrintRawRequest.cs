namespace PDV.HardwareAgent.Contracts.Requests;

public record PrintRawRequest(
    string Ip, 
    int Port, 
    string DataBase64);
