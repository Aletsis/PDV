using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Dtos;

namespace PDV.Application.Features.CashRegisters.Queries.GetLocalStationCashRegister;

public record GetLocalStationCashRegisterQuery : IRequest<CashRegisterDto?>;

public class GetLocalStationCashRegisterQueryHandler : IRequestHandler<GetLocalStationCashRegisterQuery, CashRegisterDto?>
{
    private readonly ILocalStationService _stationService;

    public GetLocalStationCashRegisterQueryHandler(ILocalStationService stationService)
    {
        _stationService = stationService;
    }

    public async Task<CashRegisterDto?> Handle(GetLocalStationCashRegisterQuery request, CancellationToken cancellationToken)
    {
        return await _stationService.GetCurrentCashRegisterAsync(cancellationToken);
    }
}
