using MediatR;
using PDV.Application.Features.Branches.Dtos;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Branches.Queries.GetBranchById;

public record GetBranchByIdQuery(Guid Id) : IRequest<BranchDto?>;

public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchDto?>
{
    private readonly IBranchRepository _repository;

    public GetBranchByIdQueryHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<BranchDto?> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (branch == null)
            return null;

        return new BranchDto(
            branch.Id,
            branch.Name,
            branch.Code,
            branch.Address,
            branch.Phone,
            branch.Email,
            branch.IsActive,
            branch.IsMainBranch
        );
    }
}
