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

public record GetInventoryConceptMappingsQuery : IRequest<List<InventoryConceptMappingDto>>;

public class GetInventoryConceptMappingsQueryHandler : IRequestHandler<GetInventoryConceptMappingsQuery, List<InventoryConceptMappingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryConceptMappingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InventoryConceptMappingDto>> Handle(GetInventoryConceptMappingsQuery request, CancellationToken cancellationToken)
    {
        var existingMappings = await _context.InventoryConceptMappings.AsNoTracking().ToListAsync(cancellationToken);

        var allSubtypes = Enum.GetValues<InventoryMovementSubtype>();
        var result = new List<InventoryConceptMappingDto>();

        foreach (var subtype in allSubtypes)
        {
            var match = existingMappings.FirstOrDefault(m => m.Subtype == subtype);
            var (defaultCode, defaultName) = GetDefaultConcept(subtype);

            result.Add(new InventoryConceptMappingDto
            {
                Id = match?.Id ?? Guid.Empty,
                Subtype = subtype,
                SubtypeName = GetSubtypeFriendlyName(subtype),
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
