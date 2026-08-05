using System;
using System.Collections.Generic;
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
    public Guid ProductId { get; init; }
    public Guid BranchId { get; init; }
    public Guid? DestinationBranchId { get; init; }
    public decimal Quantity { get; init; }
    public InventoryMovementType Type { get; init; }
    public string? Remarks { get; init; }
}

public class RegisterInventoryMovementCommandValidator : AbstractValidator<RegisterInventoryMovementCommand>
{
    public RegisterInventoryMovementCommandValidator()
    {
        RuleFor(v => v.ProductId)
            .NotEmpty().WithMessage("El ID de producto es requerido.");

        RuleFor(v => v.BranchId)
            .NotEmpty().WithMessage("La sucursal de origen es requerida.");

        RuleFor(v => v.Quantity)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

        RuleFor(v => v.Type)
            .IsInEnum().WithMessage("Tipo de movimiento inválido.");

        RuleFor(v => v.DestinationBranchId)
            .NotEmpty().When(v => v.Type == InventoryMovementType.Transfer)
            .WithMessage("La sucursal de destino es requerida para traspasos.");
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

        var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken);
        if (product == null)
            throw new KeyNotFoundException($"Producto con ID {request.ProductId} no encontrado.");

        if (request.Type != InventoryMovementType.Purchase &&
            request.Type != InventoryMovementType.AdjustmentInput &&
            request.Type != InventoryMovementType.AdjustmentOutput &&
            request.Type != InventoryMovementType.Transfer)
        {
            throw new DomainException("Tipo de movimiento no soportado en esta operación.");
        }

        var sourceBranchStock = await _context.ProductBranchStocks
            .FirstOrDefaultAsync(x => x.ProductId == request.ProductId && x.BranchId == request.BranchId, cancellationToken);

        if (sourceBranchStock == null)
        {
            sourceBranchStock = new Domain.Entities.ProductBranchStock(request.ProductId, request.BranchId, 0, 0);
            _context.ProductBranchStocks.Add(sourceBranchStock);
        }

        if (request.Type == InventoryMovementType.Transfer)
        {
            if (request.DestinationBranchId == null || request.DestinationBranchId == Guid.Empty)
                throw new DomainException("La sucursal de destino es requerida para un traspaso.");

            if (request.BranchId == request.DestinationBranchId)
                throw new DomainException("La sucursal de origen y destino no pueden ser iguales.");

            var destBranchStock = await _context.ProductBranchStocks
                .FirstOrDefaultAsync(x => x.ProductId == request.ProductId && x.BranchId == request.DestinationBranchId.Value, cancellationToken);

            if (destBranchStock == null)
            {
                destBranchStock = new Domain.Entities.ProductBranchStock(request.ProductId, request.DestinationBranchId.Value, 0, 0);
                _context.ProductBranchStocks.Add(destBranchStock);
            }

            // Validar stock si tiene control de existencia
            if (product.ControlExistencia != ControlExistencia.SinControl && sourceBranchStock.Stock < request.Quantity)
            {
                throw new DomainException($"Stock insuficiente en origen para realizar el traspaso. Disponible: {sourceBranchStock.Stock}, Requerido: {request.Quantity}");
            }

            var transferId = Guid.CreateVersion7();
            
            sourceBranchStock.ApplyMovement(-request.Quantity, InventoryMovementType.Transfer, transferId, $"Traspaso (Salida) a sucursal de destino. {request.Remarks}");
            destBranchStock.ApplyMovement(request.Quantity, InventoryMovementType.Transfer, transferId, $"Traspaso (Entrada) desde sucursal de origen. {request.Remarks}");
        }
        else if (request.Type == InventoryMovementType.AdjustmentOutput)
        {
            // Validar stock si tiene control de existencia
            if (product.ControlExistencia != ControlExistencia.SinControl && sourceBranchStock.Stock < request.Quantity)
            {
                throw new DomainException($"Stock insuficiente para realizar el ajuste de salida. Disponible: {sourceBranchStock.Stock}, Requerido: {request.Quantity}");
            }

            sourceBranchStock.ApplyMovement(-request.Quantity, InventoryMovementType.AdjustmentOutput, remarks: request.Remarks);
        }
        else
        {
            // Purchase o AdjustmentInput (Entradas positivas)
            sourceBranchStock.ApplyMovement(request.Quantity, request.Type, remarks: request.Remarks);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
