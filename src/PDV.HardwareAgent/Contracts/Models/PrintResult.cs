namespace PDV.HardwareAgent.Contracts.Models;

public class PrintResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int Attempts { get; set; }
    public long ExecutionTimeMs { get; set; }

    public static PrintResult Ok(int attempts, long elapsedMs) => new()
    {
        Success = true,
        Attempts = attempts,
        ExecutionTimeMs = elapsedMs
    };

    public static PrintResult Fail(string errorCode, string message, int attempts, long elapsedMs) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = message,
        Attempts = attempts,
        ExecutionTimeMs = elapsedMs
    };
}
