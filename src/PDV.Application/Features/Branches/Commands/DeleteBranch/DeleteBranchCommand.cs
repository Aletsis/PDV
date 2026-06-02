using MediatR;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Branches.Commands.DeleteBranch;

public record DeleteBranchCommand(Guid Id) : IRequest;

public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand>
{
    private readonly IBranchRepository _repository;

    public DeleteBranchCommandHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sucursal con ID {request.Id} no encontrada");

        if (branch.IsMainBranch)
            throw new InvalidOperationException("No se puede eliminar la sucursal principal");

        await _repository.DeleteAsync(branch, cancellationToken);
    }
}
