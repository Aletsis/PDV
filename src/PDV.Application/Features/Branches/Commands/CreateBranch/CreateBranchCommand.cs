using MediatR;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Branches.Commands.CreateBranch;

using PDV.Domain.ValueObjects;

public record CreateBranchCommand(
    string Name,
    string Code,
    string Address,
    string Phone,
    string? Email = null,
    bool IsMainBranch = false
) : IRequest<Guid>;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Guid>
{
    private readonly IBranchRepository _repository;

    public CreateBranchCommandHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        // Validar código único
        var existing = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Ya existe una sucursal con el código '{request.Code}'");

        Address? addressObj = null;
        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            addressObj = Address.Create(request.Address, "N/A", "N/A", "00000", "México");
        }

        var branch = new Branch(
            request.Name,
            request.Code,
            addressObj,
            request.Phone,
            request.Email,
            request.IsMainBranch
        );

        await _repository.AddAsync(branch, cancellationToken);
        return branch.Id;
    }
}
