using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.InventoryMovements.Commands.RegisterInventoryMovement;

public record RegisterInventoryMovementCommand : IRequest<bool>
{
    public Guid BranchId { get; init; }
    public Guid? DestinationBranchId { get; init; }
    public InventoryMovementType Type { get; init; }
    public string? Remarks { get; init; }
    public List<InventoryMovementItemCommand> Items { get; init; } = new();
}

public record InventoryMovementItemCommand
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
}

public class RegisterInventoryMovementCommandValidator : AbstractValidator<RegisterInventoryMovementCommand>
{
    public RegisterInventoryMovementCommandValidator()
    {
        RuleFor(v => v.BranchId)
            .NotEmpty().WithMessage("La sucursal de origen es requerida.");

        RuleFor(v => v.Type)
            .IsInEnum().WithMessage("Tipo de movimiento inválido.");

        RuleFor(v => v.DestinationBranchId)
            .NotEmpty().When(v => v.Type == InventoryMovementType.Transfer)
            .WithMessage("La sucursal de destino es requerida para traspasos.");

        RuleFor(v => v.Items)
            .NotEmpty().WithMessage("Debe incluir al menos un artículo.");

        RuleForEach(v => v.Items).SetValidator(new InventoryMovementItemCommandValidator());
    }
}

public class InventoryMovementItemCommandValidator : AbstractValidator<InventoryMovementItemCommand>
{
    public InventoryMovementItemCommandValidator()
    {
        RuleFor(v => v.ProductId)
            .NotEmpty().WithMessage("El ID de producto es requerido.");

        RuleFor(v => v.Quantity)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
    }
}

public class RegisterInventoryMovementCommandHandler : IRequestHandler<RegisterInventoryMovementCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RegisterInventoryMovementCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RegisterInventoryMovementCommand request, CancellationToken cancellationToken)
    {
        bool isLocalMode = false;
        if (_context is DbContext dbContext)
        {
            isLocalMode = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        if (isLocalMode)
        {
            throw new DomainException("Las operaciones de inventario no están permitidas en modo local.");
        }

        if (request.Type != InventoryMovementType.Purchase &&
            request.Type != InventoryMovementType.AdjustmentInput &&
            request.Type != InventoryMovementType.AdjustmentOutput &&
            request.Type != InventoryMovementType.Transfer)
        {
            throw new DomainException("Tipo de movimiento no soportado en esta operación.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new DomainException("Debe especificar al menos un artículo para el movimiento.");
        }

        // Generar un ReferenceId único para agrupar todo este lote/movimiento
        var batchReferenceId = Guid.CreateVersion7();

        foreach (var item in request.Items)
        {
            var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
            if (product == null)
                throw new KeyNotFoundException($"Producto con ID {item.ProductId} no encontrado.");

            var sourceBranchStock = await _context.ProductBranchStocks
                .FirstOrDefaultAsync(x => x.ProductId == item.ProductId && x.BranchId == request.BranchId, cancellationToken);

            if (sourceBranchStock == null)
            {
                sourceBranchStock = new Domain.Entities.ProductBranchStock(item.ProductId, request.BranchId, 0, 0);
                _context.ProductBranchStocks.Add(sourceBranchStock);
            }

            if (request.Type == InventoryMovementType.Transfer)
            {
                if (request.DestinationBranchId == null || request.DestinationBranchId == Guid.Empty)
                    throw new DomainException("La sucursal de destino es requerida para un traspaso.");

                if (request.BranchId == request.DestinationBranchId)
                    throw new DomainException("La sucursal de origen y destino no pueden ser iguales.");

                var destBranchStock = await _context.ProductBranchStocks
                    .FirstOrDefaultAsync(x => x.ProductId == item.ProductId && x.BranchId == request.DestinationBranchId.Value, cancellationToken);

                if (destBranchStock == null)
                {
                    destBranchStock = new Domain.Entities.ProductBranchStock(item.ProductId, request.DestinationBranchId.Value, 0, 0);
                    _context.ProductBranchStocks.Add(destBranchStock);
                }

                // Validar stock si tiene control de existencia
                if (product.ControlExistencia != ControlExistencia.SinControl && sourceBranchStock.Stock < item.Quantity)
                {
                    throw new DomainException($"Stock insuficiente en origen para realizar el traspaso del producto '{product.Name}'. Disponible: {sourceBranchStock.Stock}, Requerido: {item.Quantity}");
                }

                sourceBranchStock.ApplyMovement(-item.Quantity, InventoryMovementType.Transfer, batchReferenceId, $"Traspaso (Salida) a sucursal de destino. {request.Remarks}");
                destBranchStock.ApplyMovement(item.Quantity, InventoryMovementType.Transfer, batchReferenceId, $"Traspaso (Entrada) desde sucursal de origen. {request.Remarks}");
            }
            else if (request.Type == InventoryMovementType.AdjustmentOutput)
            {
                // Validar stock si tiene control de existencia
                if (product.ControlExistencia != ControlExistencia.SinControl && sourceBranchStock.Stock < item.Quantity)
                {
                    throw new DomainException($"Stock insuficiente para realizar el ajuste de salida del producto '{product.Name}'. Disponible: {sourceBranchStock.Stock}, Requerido: {item.Quantity}");
                }

                sourceBranchStock.ApplyMovement(-item.Quantity, InventoryMovementType.AdjustmentOutput, batchReferenceId, remarks: request.Remarks);
            }
            else
            {
                // Purchase o AdjustmentInput (Entradas positivas)
                sourceBranchStock.ApplyMovement(item.Quantity, request.Type, batchReferenceId, remarks: request.Remarks);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
