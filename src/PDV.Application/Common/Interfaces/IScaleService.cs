using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IScaleService
{
    Task<ScaleWeightDto> ReadWeightAsync(string portName, int baudRate, string protocol, CancellationToken cancellationToken = default);
}

public record ScaleWeightDto(decimal Weight, string Unit, bool IsStable, bool Success, string? ErrorMessage);
