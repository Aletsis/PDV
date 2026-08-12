using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Application.Features.InventoryDocuments.Commands.RetryInventoryDocumentSync;

public record RetryInventoryDocumentSyncCommand(Guid DocumentId) : IRequest<bool>;

public class RetryInventoryDocumentSyncCommandHandler : IRequestHandler<RetryInventoryDocumentSyncCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IComercialApiSyncService _comercialSyncService;

    public RetryInventoryDocumentSyncCommandHandler(
        IApplicationDbContext context,
        IComercialApiSyncService comercialSyncService)
    {
        _context = context;
        _comercialSyncService = comercialSyncService;
    }

    public async Task<bool> Handle(RetryInventoryDocumentSyncCommand request, CancellationToken cancellationToken)
    {
        var document = await _context.InventoryDocuments
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document == null)
            throw new KeyNotFoundException($"Documento de inventario con ID {request.DocumentId} no encontrado.");

        if (document.SyncStatus == OutboxState.Processed)
            return true;

        var sourceBranch = await _context.Branches.FindAsync(new object[] { document.BranchId }, cancellationToken);
        if (sourceBranch == null) throw new DomainException("Sucursal origen no encontrada.");

        Branch? destBranch = null;
        if (document.DestinationBranchId.HasValue)
        {
            destBranch = await _context.Branches.FindAsync(new object[] { document.DestinationBranchId.Value }, cancellationToken);
        }

        var mapping = await _context.InventoryConceptMappings
            .FirstOrDefaultAsync(m => m.Subtype == document.Subtype, cancellationToken);

        var conceptCode = mapping?.ConceptCode ?? document.Subtype.ToString();

        var productIds = document.Items.Select(i => i.ProductId).ToList();
        var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        double parsedFolio = 0;
        if (!string.IsNullOrWhiteSpace(document.Folio) && double.TryParse(document.Folio, out var f))
        {
            parsedFolio = f;
        }

        try
        {
            CreateDocumentoResultDto? result = null;

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
                    result = await _comercialSyncService.SendCompraToComercialAsync(compraDto, cancellationToken);
                    break;

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
                    result = await _comercialSyncService.SendTraspasoToComercialAsync(traspasoDto, cancellationToken);
                    break;

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
                    result = await _comercialSyncService.SendSalidaToComercialAsync(salidaDto, cancellationToken);
                    break;

                default:
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
                    result = await _comercialSyncService.SendEntradaToComercialAsync(entradaDto, cancellationToken);
                    break;
            }

            if (result != null)
            {
                document.MarkAsSynced(result.IdDocumento, result.Serie, result.Folio);

                var task = await _context.ContpaqiSyncQueues
                    .FirstOrDefaultAsync(t => t.ReferenceId == document.Id && t.Type == "InventoryDocument", cancellationToken);
                if (task != null)
                {
                    task.MarkAsProcessed();
                }

                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
        }
        catch (Exception ex)
        {
            document.MarkAsSyncFailed(ex.Message, maxAttempts: 5);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }

        return false;
    }
}
