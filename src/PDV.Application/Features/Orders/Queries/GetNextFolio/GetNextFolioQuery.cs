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

    public GetNextFolioQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<(string Series, int NextFolio)> Handle(GetNextFolioQuery request, CancellationToken cancellationToken)
    {
        int nextFolio = await _orderRepository.GetNextFolioAsync(request.BranchId, cancellationToken);
        return ("PED", nextFolio);
    }
}