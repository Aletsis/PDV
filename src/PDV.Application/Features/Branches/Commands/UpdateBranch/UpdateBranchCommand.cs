using MediatR;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Branches.Commands.UpdateBranch;

public record UpdateBranchCommand(
    Guid Id,
    string Name,
    string Address,
    string Phone,
    string? Email
) : IRequest;

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand>
{
    private readonly IBranchRepository _repository;

    public UpdateBranchCommandHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sucursal con ID {request.Id} no encontrada");

        var addressObj = PDV.Domain.ValueObjects.Address.Create(
            street: request.Address,
            city: "General",
            state: "General",
            zipCode: "00000",
            country: "México"
        );

        branch.Update(request.Name, addressObj, request.Phone, request.Email);
        await _repository.UpdateAsync(branch, cancellationToken);
    }
}
