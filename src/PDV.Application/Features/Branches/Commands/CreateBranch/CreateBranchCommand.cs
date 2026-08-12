using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Branches.Commands.CreateBranch;

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
    private readonly IApplicationDbContext _context;

    public CreateBranchCommandHandler(IBranchRepository repository, IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
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

        // Inicializar ProductBranchStock para todos los productos existentes en la nueva sucursal
        var productIds = await _context.Products.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken);
        foreach (var productId in productIds)
        {
            var branchStock = new ProductBranchStock(productId, branch.Id, 0m, 0m);
            _context.ProductBranchStocks.Add(branchStock);
        }

        if (productIds.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return branch.Id;
    }
}
