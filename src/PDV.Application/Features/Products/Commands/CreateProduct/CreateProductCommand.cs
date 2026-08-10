using System;
using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Products.Commands.CreateProduct;

public record CreateProductCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Plu { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
    public decimal Stock { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SaleType { get; set; } = "Piece";
    public string? Barcode { get; set; }
    public decimal Cost { get; set; }
    public decimal MinStock { get; set; }
    public string TaxRate { get; set; } = "Rate16";

    public string SatCode { get; set; } = string.Empty;
    public int Type { get; set; } = 1; // 1 = Producto
    public int ControlExistencia { get; set; } = 2; // 2 = ConControl
    public int? SaleUnitId { get; set; } = 0;
    public string SaleUnitName { get; set; } = "Pieza";
    public int? XmlUnitId { get; set; } = 0;
    public string Department { get; set; } = string.Empty;
    public int? Clasificacion1Id { get; set; }
    public int? Clasificacion5Id { get; set; }
    public Guid? BranchId { get; set; }
}

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(60);

        RuleFor(v => v.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(30);

        RuleFor(v => v.Price)
            .GreaterThanOrEqualTo(0);

        RuleFor(v => v.WholesalePrice)
            .GreaterThanOrEqualTo(0).When(v => v.WholesalePrice.HasValue);

        RuleFor(v => v.WholesaleMinQuantity)
            .GreaterThan(0).When(v => v.WholesaleMinQuantity.HasValue);

        RuleFor(v => v.Cost)
            .GreaterThanOrEqualTo(0);

        RuleFor(v => v.MinStock)
            .GreaterThanOrEqualTo(0);
    }
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialSyncService;

    public CreateProductCommandHandler(IApplicationDbContext context, IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var saleType = Enum.Parse<PDV.Domain.Enums.SaleType>(request.SaleType);
        var taxRate = Enum.TryParse<PDV.Domain.Enums.TaxRateType>(request.TaxRate, true, out var tr) ? tr : PDV.Domain.Enums.TaxRateType.Rate16;
        
        var productType = Enum.IsDefined(typeof(PDV.Domain.Enums.ProductType), request.Type) 
            ? (PDV.Domain.Enums.ProductType)request.Type 
            : PDV.Domain.Enums.ProductType.Producto;

        var controlExistencia = Enum.IsDefined(typeof(PDV.Domain.Enums.ControlExistencia), request.ControlExistencia) 
            ? (PDV.Domain.Enums.ControlExistencia)request.ControlExistencia 
            : PDV.Domain.Enums.ControlExistencia.ConControl;

        var entity = new Product(
            name: request.Name,
            code: request.Code,
            price: request.Price,
            saleType: saleType,
            taxRate: taxRate,
            category: request.Category,
            cost: request.Cost,
            plu: request.Plu,
            barcode: request.Barcode,
            description: request.Description,
            wholesalePrice: request.WholesalePrice,
            wholesaleMinQuantity: request.WholesaleMinQuantity,
            satCode: request.SatCode,
            type: productType,
            controlExistencia: controlExistencia,
            saleUnitId: request.SaleUnitId,
            saleUnitName: request.SaleUnitName,
            xmlUnitId: request.XmlUnitId,
            department: request.Department,
            clasificacion1Id: request.Clasificacion1Id,
            clasificacion5Id: request.Clasificacion5Id);

        _context.Products.Add(entity);

        // Resolver sucursal activa/actual
        var activeBranchId = request.BranchId;
        if (activeBranchId == null || activeBranchId == Guid.Empty)
        {
            var activeShift = await _context.Shifts
                .Include(s => s.CashRegister)
                .FirstOrDefaultAsync(s => s.Status == PDV.Domain.Enums.ShiftStatus.Open, cancellationToken);
            activeBranchId = activeShift?.CashRegister?.BranchId;

            if (activeBranchId == null || activeBranchId == Guid.Empty)
            {
                var firstBranch = await _context.Branches.FirstOrDefaultAsync(cancellationToken);
                activeBranchId = firstBranch?.Id;
            }
        }

        // Inicializar stock en todas las sucursales
        var branches = await _context.Branches.ToListAsync(cancellationToken);
        foreach (var b in branches)
        {
            var branchStock = new ProductBranchStock(entity.Id, b.Id, 0m, request.MinStock);
            if (b.Id == activeBranchId && request.Stock > 0)
            {
                branchStock.ApplyMovement(request.Stock, PDV.Domain.Enums.InventoryMovementType.InitialInventory, remarks: "Inventario inicial");
            }
            _context.ProductBranchStocks.Add(branchStock);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Sincronizar de forma diferida con Comercial si estamos en el servidor (no SQLite)
        if (_context is DbContext dbContext && dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == false)
        {
            try
            {
                var queueItem = new ContpaqiSyncQueue(entity.Id, "Product", "Create");
                _context.ContpaqiSyncQueues.Add(queueItem);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                // Resiliencia
            }
        }

        return entity.Id;
    }
}
