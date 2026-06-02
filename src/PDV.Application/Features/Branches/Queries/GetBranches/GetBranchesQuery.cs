using MediatR;
using PDV.Application.Features.Branches.Dtos;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Branches.Queries.GetBranches;

public record GetBranchesQuery(bool IncludeInactive = false) : IRequest<List<BranchDto>>;

public class GetBranchesQueryHandler : IRequestHandler<GetBranchesQuery, List<BranchDto>>
{
    private readonly IBranchRepository _repository;

    public GetBranchesQueryHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var branches = request.IncludeInactive 
            ? await _repository.GetAllAsync(cancellationToken)
            : await _repository.GetAllActiveAsync(cancellationToken);
        
        return branches.Select(b => new BranchDto(
            b.Id,
            b.Name,
            b.Code,
            b.Address,
            b.Phone,
            b.Email,
            b.IsActive,
            b.IsMainBranch
        )).ToList();
    }
}
