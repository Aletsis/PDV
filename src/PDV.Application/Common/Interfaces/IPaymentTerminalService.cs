using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IPaymentTerminalService
{
    Task<PaymentResultDto> ProcessPaymentAsync(decimal amount, string reference, string transactionType, string protocol, string portName, CancellationToken cancellationToken = default);
    Task<PaymentResultDto> CancelPaymentAsync(string transactionId, string protocol, string portName, CancellationToken cancellationToken = default);
}

public record PaymentResultDto(bool Success, string TransactionId, string AuthorizationCode, string Brand, string LastFour, string Message, string? ErrorCode);
