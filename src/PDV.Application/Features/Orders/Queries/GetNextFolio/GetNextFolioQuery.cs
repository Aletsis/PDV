using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Orders.Queries.GetNextFolio;

public record GetNextFolioQuery(Guid BranchId) : IRequest<(string Series, int NextFolio)>;

public class GetNextFolioQueryHandler : IRequestHandler<GetNextFolioQuery, (string Series, int NextFolio)>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IBranchRepository _branchRepository;

    public GetNextFolioQueryHandler(IOrderRepository orderRepository, IBranchRepository branchRepository)
    {
        _orderRepository = orderRepository;
        _branchRepository = branchRepository;
    }

    public async Task<(string Series, int NextFolio)> Handle(GetNextFolioQuery request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken);
        string series = branch?.GetEffectiveOrderSeries() ?? "PED";
        int nextFolio = await _orderRepository.GetNextFolioAsync(request.BranchId, cancellationToken);
        return (series, nextFolio);
    }
}