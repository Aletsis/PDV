using System;
using FluentValidation;
using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Products.Commands.UpdateProduct;

public record UpdateProductCommand : IRequest
{
    public Guid Id { get; set; }
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
}

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(v => v.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(50);

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

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialSyncService;

    public UpdateProductCommandHandler(IApplicationDbContext context, IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Products.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            throw new Exception("Entity not found");
        }

        var productType = Enum.IsDefined(typeof(PDV.Domain.Enums.ProductType), request.Type) 
            ? (PDV.Domain.Enums.ProductType)request.Type 
            : PDV.Domain.Enums.ProductType.Producto;

        var controlExistencia = Enum.IsDefined(typeof(PDV.Domain.Enums.ControlExistencia), request.ControlExistencia) 
            ? (PDV.Domain.Enums.ControlExistencia)request.ControlExistencia 
            : PDV.Domain.Enums.ControlExistencia.ConControl;

        // Usar métodos de dominio para actualizar
        entity.UpdateInfo(
            name: request.Name, 
            description: request.Description, 
            category: request.Category,
            satCode: request.SatCode,
            type: productType,
            controlExistencia: controlExistencia,
            saleUnitId: request.SaleUnitId,
            saleUnitName: request.SaleUnitName,
            xmlUnitId: request.XmlUnitId,
            department: request.Department,
            clasificacion1Id: request.Clasificacion1Id,
            clasificacion5Id: request.Clasificacion5Id);
        
        if (entity.Code != request.Code)
        {
            entity.ChangeCode(request.Code);
        }
        
        entity.UpdatePrice(request.Price);
        entity.UpdateWholesalePrice(request.WholesalePrice, request.WholesaleMinQuantity);
        entity.AdjustStock(request.Stock);
        entity.UpdatePlu(request.Plu);
        entity.UpdateBarcode(request.Barcode);
        entity.UpdateCost(request.Cost);
        entity.UpdateMinStock(request.MinStock);

        if (Enum.TryParse<PDV.Domain.Enums.SaleType>(request.SaleType, true, out var saleType))
        {
            entity.ChangeSaleType(saleType);
        }

        if (Enum.TryParse<PDV.Domain.Enums.TaxRateType>(request.TaxRate, true, out var taxRate))
        {
            entity.UpdateTaxRate(taxRate);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Sincronizar en tiempo real con Comercial
        try
        {
            await _comercialSyncService.UpdateProductInComercialAsync(entity, cancellationToken);
        }
        catch (Exception)
        {
            // Resiliencia: Si falla el API Comercial, no detenemos la operación local del PDV.
        }
    }
}
