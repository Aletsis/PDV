using MediatR;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.CashRegisters.Commands.BindLocalStationCashRegister;

public record BindLocalStationCashRegisterCommand(Guid CashRegisterId) : IRequest<bool>;

public class BindLocalStationCashRegisterCommandHandler : IRequestHandler<BindLocalStationCashRegisterCommand, bool>
{
    private readonly ILocalStationService _stationService;

    public BindLocalStationCashRegisterCommandHandler(ILocalStationService stationService)
    {
        _stationService = stationService;
    }

    public async Task<bool> Handle(BindLocalStationCashRegisterCommand request, CancellationToken cancellationToken)
    {
        await _stationService.SetAssignedCashRegisterIdAsync(request.CashRegisterId, cancellationToken);
        return true;
    }
}
