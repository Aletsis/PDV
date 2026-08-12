using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.InventoryDocuments.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.InventoryDocuments.Queries.GetInventoryConceptMappings;

public record GetInventoryConceptMappingsQuery(Guid BranchId) : IRequest<List<InventoryConceptMappingDto>>;

public class GetInventoryConceptMappingsQueryHandler : IRequestHandler<GetInventoryConceptMappingsQuery, List<InventoryConceptMappingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryConceptMappingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InventoryConceptMappingDto>> Handle(GetInventoryConceptMappingsQuery request, CancellationToken cancellationToken)
    {
        var existingMappings = await _context.InventoryConceptMappings
            .Where(m => m.BranchId == request.BranchId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var allBranches = await _context.Branches
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = new List<InventoryConceptMappingDto>();

        // 1. Subtipos de Compras
        var purchaseSubtypes = new[]
        {
            InventoryMovementSubtype.PurchaseGroceries,
            InventoryMovementSubtype.PurchasePettyCash,
            InventoryMovementSubtype.PurchaseStandard,
            InventoryMovementSubtype.PurchaseFixedExpenses,
            InventoryMovementSubtype.PurchaseSuppliers
        };

        foreach (var subtype in purchaseSubtypes)
        {
            var match = existingMappings.FirstOrDefault(m => m.MovementType == InventoryMovementType.Purchase && m.Subtype == subtype);
            var (defaultCode, defaultName) = GetDefaultConcept(subtype);

            result.Add(new InventoryConceptMappingDto
            {
                Id = match?.Id ?? Guid.Empty,
                BranchId = request.BranchId,
                MovementType = InventoryMovementType.Purchase,
                Subtype = subtype,
                SubtypeName = GetSubtypeFriendlyName(subtype),
                DisplayLabel = GetSubtypeFriendlyName(subtype),
                ConceptCode = match?.ConceptCode ?? defaultCode,
                ConceptName = match?.ConceptName ?? defaultName,
                DefaultSeries = match?.DefaultSeries
            });
        }

        // 2. Traspasos hacia cada sucursal de destino por cada uno de los 3 subtipos (Abarrotes, Almacén, Insumos)
        var transferSubtypes = new[]
        {
            (InventoryMovementSubtype.TransferGroceries, "Abarrotes", "ABA"),
            (InventoryMovementSubtype.TransferWarehouse, "Almacén", "ALM"),
            (InventoryMovementSubtype.TransferSupplies, "Insumos", "INS")
        };

        var destinationBranches = allBranches.Where(b => b.Id != request.BranchId).ToList();
        foreach (var dest in destinationBranches)
        {
            foreach (var (subt, subtName, subtPrefix) in transferSubtypes)
            {
                var match = existingMappings.FirstOrDefault(m => 
                    m.MovementType == InventoryMovementType.Transfer && 
                    m.DestinationBranchId == dest.Id && 
                    m.Subtype == subt);

                var defaultCode = $"TRAS-{subtPrefix}-{dest.Code.ToUpper()}";
                var defaultName = $"Traspaso {subtName} hacia {dest.Name}";

                result.Add(new InventoryConceptMappingDto
                {
                    Id = match?.Id ?? Guid.Empty,
                    BranchId = request.BranchId,
                    MovementType = InventoryMovementType.Transfer,
                    Subtype = subt,
                    SubtypeName = $"Traspaso {subtName} hacia {dest.Name}",
                    DestinationBranchId = dest.Id,
                    DestinationBranchName = dest.Name,
                    DisplayLabel = $"Hacia {dest.Name} ({dest.Code}) — {subtName}",
                    ConceptCode = match?.ConceptCode ?? defaultCode,
                    ConceptName = match?.ConceptName ?? defaultName,
                    DefaultSeries = match?.DefaultSeries
                });
            }
        }

        // 3. Ajustes y Otros
        var adjustmentSubtypes = new[]
        {
            (InventoryMovementType.AdjustmentInput, InventoryMovementSubtype.AdjustmentInputGeneral),
            (InventoryMovementType.AdjustmentOutput, InventoryMovementSubtype.AdjustmentOutputGeneral),
            (InventoryMovementType.InitialInventory, InventoryMovementSubtype.InitialInventory)
        };

        foreach (var (movType, subtype) in adjustmentSubtypes)
        {
            var match = existingMappings.FirstOrDefault(m => m.MovementType == movType && m.Subtype == subtype);
            var (defaultCode, defaultName) = GetDefaultConcept(subtype);

            result.Add(new InventoryConceptMappingDto
            {
                Id = match?.Id ?? Guid.Empty,
                BranchId = request.BranchId,
                MovementType = movType,
                Subtype = subtype,
                SubtypeName = GetSubtypeFriendlyName(subtype),
                DisplayLabel = GetSubtypeFriendlyName(subtype),
                ConceptCode = match?.ConceptCode ?? defaultCode,
                ConceptName = match?.ConceptName ?? defaultName,
                DefaultSeries = match?.DefaultSeries
            });
        }

        return result;
    }

    private static (string Code, string Name) GetDefaultConcept(InventoryMovementSubtype subtype)
    {
        return subtype switch
        {
            InventoryMovementSubtype.PurchaseGroceries => ("COMP-ABA", "Compras Abarrotes"),
            InventoryMovementSubtype.PurchasePettyCash => ("COMP-CCH", "Compras Caja Chica"),
            InventoryMovementSubtype.PurchaseStandard => ("COMP", "Compras"),
            InventoryMovementSubtype.PurchaseFixedExpenses => ("COMP-GFIJ", "Compras Gastos Fijos"),
            InventoryMovementSubtype.PurchaseSuppliers => ("COMP-PROV", "Compras Proveedores"),

            InventoryMovementSubtype.TransferGroceries => ("TRAS-ABA", "Traspaso Abarrotes"),
            InventoryMovementSubtype.TransferWarehouse => ("TRAS-ALM", "Traspaso Almacén"),
            InventoryMovementSubtype.TransferSupplies => ("TRAS-INS", "Traspaso Insumos"),

            InventoryMovementSubtype.AdjustmentInputGeneral => ("AJU-ENT", "Ajuste de Entrada"),
            InventoryMovementSubtype.AdjustmentOutputGeneral => ("AJU-SAL", "Ajuste de Salida / Merma"),
            InventoryMovementSubtype.InitialInventory => ("INV-INI", "Inventario Inicial"),
            _ => ("GENERAL", "Concepto General")
        };
    }

    private static string GetSubtypeFriendlyName(InventoryMovementSubtype subtype)
    {
        return subtype switch
        {
            InventoryMovementSubtype.PurchaseGroceries => "Compras - Abarrotes",
            InventoryMovementSubtype.PurchasePettyCash => "Compras - Caja Chica",
            InventoryMovementSubtype.PurchaseStandard => "Compras - General",
            InventoryMovementSubtype.PurchaseFixedExpenses => "Compras - Gastos Fijos",
            InventoryMovementSubtype.PurchaseSuppliers => "Compras - Proveedores",

            InventoryMovementSubtype.TransferGroceries => "Traspaso - Abarrotes",
            InventoryMovementSubtype.TransferWarehouse => "Traspaso - Almacén",
            InventoryMovementSubtype.TransferSupplies => "Traspaso - Insumos",

            InventoryMovementSubtype.AdjustmentInputGeneral => "Ajuste de Entrada - General",
            InventoryMovementSubtype.AdjustmentOutputGeneral => "Ajuste de Salida / Merma",
            InventoryMovementSubtype.InitialInventory => "Inventario Inicial",
            _ => subtype.ToString()
        };
    }
}
