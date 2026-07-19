using System;
using System.Threading;
using System.Threading.Tasks;
using PDV.Application.Common.Interfaces;

namespace PDV.HardwareAgent.Services;

public class PaymentTerminalService : IPaymentTerminalService
{
    public async Task<PaymentResultDto> ProcessPaymentAsync(decimal amount, string reference, string transactionType, string protocol, string portName, CancellationToken cancellationToken = default)
    {
        if (string.Equals(protocol, "Mock", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(portName, "MOCK", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // Simulate card prompt and processing delay
                await Task.Delay(2000, cancellationToken);

                if (amount <= 0)
                {
                    return new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, "MONTO INVALIDO", "INVALID_AMOUNT");
                }

                // Simulating a decline for amount ending in .99 for testing purposes
                if (amount % 1 == 0.99m)
                {
                    return new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, "FONDOS INSUFICIENTES", "DECLINED");
                }

                string transactionId = "TX" + DateTime.UtcNow.Ticks.ToString()[..10];
                string authCode = Random.Shared.Next(100000, 999999).ToString();
                string[] brands = { "VISA", "MASTERCARD", "AMEX" };
                string brand = brands[Random.Shared.Next(brands.Length)];
                string lastFour = Random.Shared.Next(1000, 9999).ToString();

                return new PaymentResultDto(true, transactionId, authCode, brand, lastFour, "TRANSACCION APROBADA", null);
            }
            catch (OperationCanceledException)
            {
                return new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, "TRANSACCION CANCELADA", "CANCELLED");
            }
        }

        return new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, $"Protocol '{protocol}' not supported.", "UNSUPPORTED_PROTOCOL");
    }

    public async Task<PaymentResultDto> CancelPaymentAsync(string transactionId, string protocol, string portName, CancellationToken cancellationToken = default)
    {
        if (string.Equals(protocol, "Mock", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(portName, "MOCK", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await Task.Delay(1000, cancellationToken);
                return new PaymentResultDto(true, transactionId, "000000", "", "", "DEVOLUCION APROBADA", null);
            }
            catch (OperationCanceledException)
            {
                return new PaymentResultDto(false, transactionId, string.Empty, string.Empty, string.Empty, "DEVOLUCION CANCELADA", "CANCELLED");
            }
        }

        return new PaymentResultDto(false, transactionId, string.Empty, string.Empty, string.Empty, $"Protocol '{protocol}' not supported.", "UNSUPPORTED_PROTOCOL");
    }
}
