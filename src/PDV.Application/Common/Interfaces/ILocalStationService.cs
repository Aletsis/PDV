using PDV.Application.Features.CashRegisters.Dtos;

namespace PDV.Application.Common.Interfaces;

public interface ILocalStationService
{
    Task<Guid?> GetAssignedCashRegisterIdAsync(CancellationToken cancellationToken = default);
    Task SetAssignedCashRegisterIdAsync(Guid cashRegisterId, CancellationToken cancellationToken = default);
    Task<CashRegisterDto?> GetCurrentCashRegisterAsync(CancellationToken cancellationToken = default);
    Task ClearAssignedCashRegisterIdAsync(CancellationToken cancellationToken = default);
}
