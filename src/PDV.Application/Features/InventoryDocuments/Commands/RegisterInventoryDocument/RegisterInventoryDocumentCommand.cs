using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.InventoryDocuments.Commands.RegisterInventoryDocument;

public record RegisterInventoryDocumentCommand : IRequest<RegisterInventoryDocumentResult>
{
    public Guid BranchId { get; init; }
    public Guid? DestinationBranchId { get; init; }
    public InventoryMovementType Type { get; init; }
    public InventoryMovementSubtype Subtype { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierCode { get; init; }
    public string? SupplierName { get; init; }
    public string? Series { get; init; }
    public string? Folio { get; init; }
    public string? Reference { get; init; }
    public string? Remarks { get; init; }
    public List<InventoryDocumentItemInputDto> Items { get; init; } = new();
}

public class InventoryDocumentItemInputDto
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public string? Remarks { get; init; }
}

public class RegisterInventoryDocumentResult
{
    public bool Success { get; set; }
    public Guid DocumentId { get; set; }
    public string Series { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public OutboxState SyncStatus { get; set; }
    public string? Message { get; set; }
}

public class RegisterInventoryDocumentCommandValidator : AbstractValidator<RegisterInventoryDocumentCommand>
{
    public RegisterInventoryDocumentCommandValidator()
    {
        RuleFor(v => v.BranchId)
            .NotEmpty().WithMessage("La sucursal de origen es obligatoria.");

        RuleFor(v => v.Type)
            .IsInEnum().WithMessage("Tipo de movimiento inválido.");

        RuleFor(v => v.DestinationBranchId)
            .NotEmpty().When(v => v.Type == InventoryMovementType.Transfer)
            .WithMessage("La sucursal de destino es obligatoria para traspasos.");

        RuleFor(v => v.SupplierId)
            .NotEmpty().When(v => v.Type == InventoryMovementType.Purchase && string.IsNullOrWhiteSpace(v.SupplierCode))
            .WithMessage("El proveedor es obligatorio para compras.");

        RuleFor(v => v.Items)
            .NotEmpty().WithMessage("Debe incluir al menos un artículo en el documento.");

        RuleForEach(v => v.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("El ID de producto es obligatorio.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
        });
    }
}

public class RegisterInventoryDocumentCommandHandler : IRequestHandler<RegisterInventoryDocumentCommand, RegisterInventoryDocumentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IComercialApiSyncService _comercialSyncService;

    public RegisterInventoryDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<RegisterInventoryDocumentResult> Handle(RegisterInventoryDocumentCommand request, CancellationToken cancellationToken)
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

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new DomainException("Debe especificar al menos un artículo para el documento.");
        }

        // Obtener sucursal origen y destino
        var sourceBranch = await _context.Branches.FindAsync(new object[] { request.BranchId }, cancellationToken);
        if (sourceBranch == null) throw new KeyNotFoundException($"Sucursal de origen con ID {request.BranchId} no encontrada.");

        Branch? destBranch = null;
        if (request.DestinationBranchId.HasValue && request.DestinationBranchId.Value != Guid.Empty)
        {
            destBranch = await _context.Branches.FindAsync(new object[] { request.DestinationBranchId.Value }, cancellationToken);
            if (destBranch == null) throw new KeyNotFoundException($"Sucursal de destino con ID {request.DestinationBranchId.Value} no encontrada.");
        }

        // Obtener datos de proveedor si aplica
        string? supplierCode = request.SupplierCode;
        string? supplierName = request.SupplierName;
        if (request.SupplierId.HasValue && request.SupplierId.Value != Guid.Empty)
        {
            var supplier = await _context.Suppliers.FindAsync(new object[] { request.SupplierId.Value }, cancellationToken);
            if (supplier != null)
            {
                supplierCode = supplier.Code;
                supplierName = supplier.Name;
            }
        }

        // En Compras: la serie es el código de proveedor
        string series = request.Series ?? string.Empty;
        if (request.Type == InventoryMovementType.Purchase && !string.IsNullOrWhiteSpace(supplierCode))
        {
            series = supplierCode;
        }

        var currentUser = _currentUserService.UserName ?? "SISTEMA";

        var document = new InventoryDocument(
            branchId: request.BranchId,
            type: request.Type,
            subtype: request.Subtype,
            createdBy: currentUser,
            destinationBranchId: request.DestinationBranchId,
            supplierId: request.SupplierId,
            supplierCode: supplierCode,
            supplierName: supplierName,
            series: series,
            folio: request.Folio,
            reference: request.Reference,
            remarks: request.Remarks);

        _context.InventoryDocuments.Add(document);

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
                sourceBranchStock = new ProductBranchStock(item.ProductId, request.BranchId, 0, 0);
                _context.ProductBranchStocks.Add(sourceBranchStock);
            }

            if (request.Type == InventoryMovementType.Transfer)
            {
                if (destBranch == null)
                    throw new DomainException("La sucursal de destino es requerida para un traspaso.");

                var destBranchStock = await _context.ProductBranchStocks
                    .FirstOrDefaultAsync(x => x.ProductId == item.ProductId && x.BranchId == destBranch.Id, cancellationToken);

                if (destBranchStock == null)
                {
                    destBranchStock = new ProductBranchStock(item.ProductId, destBranch.Id, 0, 0);
                    _context.ProductBranchStocks.Add(destBranchStock);
                }

                if (product.ControlExistencia != ControlExistencia.SinControl && sourceBranchStock.Stock < item.Quantity)
                {
                    throw new DomainException($"Stock insuficiente en origen para traspaso del producto '{product.Name}'. Disponible: {sourceBranchStock.Stock}, Requerido: {item.Quantity}");
                }

                sourceBranchStock.ApplyMovement(
                    quantity: -item.Quantity,
                    type: InventoryMovementType.Transfer,
                    referenceId: batchReferenceId,
                    remarks: $"Traspaso (Salida) a {destBranch.Name}. {request.Remarks}".Trim(),
                    documentId: document.Id,
                    subtype: request.Subtype);

                destBranchStock.ApplyMovement(
                    quantity: item.Quantity,
                    type: InventoryMovementType.Transfer,
                    referenceId: batchReferenceId,
                    remarks: $"Traspaso (Entrada) desde {sourceBranch.Name}. {request.Remarks}".Trim(),
                    documentId: document.Id,
                    subtype: request.Subtype);
            }
            else if (request.Type == InventoryMovementType.AdjustmentOutput)
            {
                if (product.ControlExistencia != ControlExistencia.SinControl && sourceBranchStock.Stock < item.Quantity)
                {
                    throw new DomainException($"Stock insuficiente para ajuste de salida del producto '{product.Name}'. Disponible: {sourceBranchStock.Stock}, Requerido: {item.Quantity}");
                }

                sourceBranchStock.ApplyMovement(
                    quantity: -item.Quantity,
                    type: InventoryMovementType.AdjustmentOutput,
                    referenceId: batchReferenceId,
                    remarks: request.Remarks,
                    documentId: document.Id,
                    subtype: request.Subtype);
            }
            else
            {
                // Purchase, AdjustmentInput, InitialInventory
                sourceBranchStock.ApplyMovement(
                    quantity: item.Quantity,
                    type: request.Type,
                    referenceId: batchReferenceId,
                    remarks: request.Remarks,
                    documentId: document.Id,
                    subtype: request.Subtype);
            }

            document.AddItem(item.ProductId, item.Quantity, item.UnitCost, item.Remarks);
        }

        // Encolar para resiliencia en segundo plano
        var syncTask = new ContpaqiSyncQueue(document.Id, "InventoryDocument", "Create");
        _context.ContpaqiSyncQueues.Add(syncTask);

        await _context.SaveChangesAsync(cancellationToken);

        // Intento Inmediato de sincronización con CONTPAQi Comercial
        try
        {
            var syncResult = await ExecuteSyncWithComercialAsync(document, sourceBranch, destBranch, cancellationToken);
            if (syncResult != null)
            {
                document.MarkAsSynced(syncResult.IdDocumento, syncResult.Serie, syncResult.Folio);
                syncTask.MarkAsProcessed();
                await _context.SaveChangesAsync(cancellationToken);

                return new RegisterInventoryDocumentResult
                {
                    Success = true,
                    DocumentId = document.Id,
                    Series = document.Series,
                    Folio = document.Folio,
                    SyncStatus = OutboxState.Processed,
                    Message = $"Documento registrado y sincronizado exitosamente con CONTPAQi (Folio: {document.Series} {document.Folio})."
                };
            }
        }
        catch (Exception ex)
        {
            var errorMsg = ex.Message;
            document.MarkAsSyncFailed(errorMsg, maxAttempts: 5);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new RegisterInventoryDocumentResult
        {
            Success = true,
            DocumentId = document.Id,
            Series = document.Series,
            Folio = document.Folio,
            SyncStatus = OutboxState.Pending,
            Message = "Documento registrado localmente. Sincronización con CONTPAQi encolada en segundo plano."
        };
    }

    private async Task<CreateDocumentoResultDto?> ExecuteSyncWithComercialAsync(
        InventoryDocument document,
        Branch sourceBranch,
        Branch? destBranch,
        CancellationToken cancellationToken)
    {
        // Buscar mapeo de concepto por sucursal y tipo/destino
        InventoryConceptMapping? mapping = null;
        if (document.Type == InventoryMovementType.Transfer)
        {
            mapping = await _context.InventoryConceptMappings
                .FirstOrDefaultAsync(m => m.BranchId == document.BranchId &&
                                          m.MovementType == InventoryMovementType.Transfer &&
                                          m.DestinationBranchId == document.DestinationBranchId &&
                                          m.Subtype == document.Subtype, cancellationToken);
        }
        else
        {
            mapping = await _context.InventoryConceptMappings
                .FirstOrDefaultAsync(m => m.BranchId == document.BranchId &&
                                          m.MovementType == document.Type &&
                                          m.Subtype == document.Subtype, cancellationToken);
        }

        var conceptCode = mapping?.ConceptCode;
        if (string.IsNullOrWhiteSpace(conceptCode))
        {
            conceptCode = document.Type switch
            {
                InventoryMovementType.Transfer => document.Subtype switch
                {
                    InventoryMovementSubtype.TransferGroceries => $"TRAS-ABA-{(destBranch?.Code ?? "DEST").ToUpper()}",
                    InventoryMovementSubtype.TransferWarehouse => $"TRAS-ALM-{(destBranch?.Code ?? "DEST").ToUpper()}",
                    InventoryMovementSubtype.TransferSupplies => $"TRAS-INS-{(destBranch?.Code ?? "DEST").ToUpper()}",
                    _ => $"TRAS-{(destBranch?.Code ?? "DEST").ToUpper()}"
                },
                InventoryMovementType.Purchase => document.Subtype switch
                {
                    InventoryMovementSubtype.PurchaseGroceries => "COMP-ABA",
                    InventoryMovementSubtype.PurchasePettyCash => "COMP-CCH",
                    InventoryMovementSubtype.PurchaseStandard => "COMP",
                    InventoryMovementSubtype.PurchaseFixedExpenses => "COMP-GFIJ",
                    InventoryMovementSubtype.PurchaseSuppliers => "COMP-PROV",
                    _ => "COMP"
                },
                InventoryMovementType.AdjustmentOutput => "AJU-SAL",
                InventoryMovementType.InitialInventory => "INV-INI",
                _ => "AJU-ENT"
            };
        }

        // Cargar productos para partidas
        var productIds = document.Items.Select(i => i.ProductId).ToList();
        var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        double parsedFolio = 0;
        if (!string.IsNullOrWhiteSpace(document.Folio) && double.TryParse(document.Folio, out var f))
        {
            parsedFolio = f;
        }

        switch (document.Type)
        {
            case InventoryMovementType.Purchase:
                var compraDto = new SendCompraDto
                {
                    CodigoConcepto = conceptCode,
                    Serie = document.Series,
                    Folio = parsedFolio,
                    CodigoProveedor = document.SupplierCode ?? string.Empty,
                    Referencia = document.Reference ?? string.Empty,
                    Observaciones = document.Remarks ?? string.Empty,
                    CodigoAlmacen = sourceBranch.Code,
                    Partidas = document.Items.Select(i => new CompraPartidaSyncDto
                    {
                        CodigoProducto = products.TryGetValue(i.ProductId, out var prod) ? prod.Code : string.Empty,
                        Unidades = (double)i.Quantity,
                        PrecioUnitario = (double)i.UnitCost
                    }).ToList()
                };
                return await _comercialSyncService.SendCompraToComercialAsync(compraDto, cancellationToken);

            case InventoryMovementType.Transfer:
                var traspasoDto = new SendTraspasoDto
                {
                    CodigoConcepto = conceptCode,
                    Serie = document.Series,
                    Folio = parsedFolio,
                    CodigoAlmacenOrigen = sourceBranch.Code,
                    CodigoAlmacenDestino = destBranch?.Code ?? string.Empty,
                    Referencia = document.Reference ?? string.Empty,
                    Observaciones = document.Remarks ?? string.Empty,
                    Partidas = document.Items.Select(i => new TraspasoPartidaSyncDto
                    {
                        CodigoProducto = products.TryGetValue(i.ProductId, out var prod) ? prod.Code : string.Empty,
                        Unidades = (double)i.Quantity
                    }).ToList()
                };
                return await _comercialSyncService.SendTraspasoToComercialAsync(traspasoDto, cancellationToken);

            case InventoryMovementType.AdjustmentOutput:
                var salidaDto = new SendSalidaDto
                {
                    CodigoConcepto = conceptCode,
                    Serie = document.Series,
                    Referencia = document.Reference ?? string.Empty,
                    Observaciones = document.Remarks ?? string.Empty,
                    Partidas = document.Items.Select(i => new SalidaPartidaSyncDto
                    {
                        CodigoProducto = products.TryGetValue(i.ProductId, out var prod) ? prod.Code : string.Empty,
                        CodigoAlmacen = sourceBranch.Code,
                        Unidades = (double)i.Quantity
                    }).ToList()
                };
                return await _comercialSyncService.SendSalidaToComercialAsync(salidaDto, cancellationToken);

            default:
                // AdjustmentInput / InitialInventory
                var entradaDto = new SendEntradaDto
                {
                    CodigoConcepto = conceptCode,
                    Serie = document.Series,
                    Referencia = document.Reference ?? string.Empty,
                    Observaciones = document.Remarks ?? string.Empty,
                    Partidas = document.Items.Select(i => new EntradaPartidaSyncDto
                    {
                        CodigoProducto = products.TryGetValue(i.ProductId, out var prod) ? prod.Code : string.Empty,
                        CodigoAlmacen = sourceBranch.Code,
                        Unidades = (double)i.Quantity,
                        Costo = (double)i.UnitCost
                    }).ToList()
                };
                return await _comercialSyncService.SendEntradaToComercialAsync(entradaDto, cancellationToken);
        }
    }
}
