using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using PDV.Application.Common.Interfaces;

namespace PDV.WebUI.Services;

public class WebUIProxyPaymentTerminal : IPaymentTerminalService
{
    private readonly IJSRuntime _jsRuntime;

    public WebUIProxyPaymentTerminal(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<PaymentResultDto> ProcessPaymentAsync(decimal amount, string reference, string transactionType, string protocol, string portName, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<PaymentResultDto>(
                "posProcessPayment", 
                cancellationToken, 
                amount, 
                reference, 
                transactionType, 
                protocol, 
                portName);
            
            return result;
        }
        catch (System.Exception ex)
        {
            return new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, $"Proxy error: {ex.Message}", "PROXY_ERROR");
        }
    }

    public async Task<PaymentResultDto> CancelPaymentAsync(string transactionId, string protocol, string portName, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<PaymentResultDto>(
                "posCancelPayment", 
                cancellationToken, 
                transactionId, 
                protocol, 
                portName);
            
            return result;
        }
        catch (System.Exception ex)
        {
            return new PaymentResultDto(false, transactionId, string.Empty, string.Empty, string.Empty, $"Proxy error: {ex.Message}", "PROXY_ERROR");
        }
    }
}
