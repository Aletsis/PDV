using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using PDV.Application.Common.Interfaces;

namespace PDV.WebUI.Services;

public class WebUIProxyScale : IScaleService
{
    private readonly IJSRuntime _jsRuntime;

    public WebUIProxyScale(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<ScaleWeightDto> ReadWeightAsync(string portName, int baudRate, string protocol, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<ScaleWeightDto>(
                "posReadWeight", 
                cancellationToken, 
                portName, 
                baudRate, 
                protocol);
            
            return result;
        }
        catch (System.Exception ex)
        {
            return new ScaleWeightDto(0, "kg", false, false, $"Proxy error: {ex.Message}");
        }
    }
}
