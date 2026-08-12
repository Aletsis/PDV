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

namespace PDV.Application.Features.InventoryDocuments.Commands.UpdateInventoryConceptMappings;

public record UpdateInventoryConceptMappingsCommand(Guid BranchId, List<InventoryConceptMappingDto> Mappings) : IRequest<bool>;

public class UpdateInventoryConceptMappingsCommandHandler : IRequestHandler<UpdateInventoryConceptMappingsCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateInventoryConceptMappingsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateInventoryConceptMappingsCommand request, CancellationToken cancellationToken)
    {
        if (request.Mappings == null || request.Mappings.Count == 0) return true;

        var currentMappings = await _context.InventoryConceptMappings
            .Where(m => m.BranchId == request.BranchId)
            .ToListAsync(cancellationToken);

        foreach (var item in request.Mappings)
        {
            if (string.IsNullOrWhiteSpace(item.ConceptCode)) continue;

            if (item.MovementType == InventoryMovementType.Transfer)
            {
                if (item.DestinationBranchId == null || item.DestinationBranchId == Guid.Empty) continue;

                var mapping = currentMappings.FirstOrDefault(m => 
                    m.MovementType == InventoryMovementType.Transfer && 
                    m.DestinationBranchId == item.DestinationBranchId &&
                    m.Subtype == item.Subtype);

                if (mapping != null)
                {
                    mapping.UpdateMapping(item.ConceptCode, item.ConceptName, item.DefaultSeries);
                }
                else
                {
                    var newMapping = new InventoryConceptMapping(
                        request.BranchId,
                        item.DestinationBranchId.Value,
                        item.ConceptCode,
                        string.IsNullOrWhiteSpace(item.ConceptName) ? item.SubtypeName : item.ConceptName,
                        item.DefaultSeries,
                        item.Subtype
                    );
                    _context.InventoryConceptMappings.Add(newMapping);
                }
            }
            else
            {
                var mapping = currentMappings.FirstOrDefault(m => 
                    m.MovementType == item.MovementType && 
                    m.Subtype == item.Subtype);

                if (mapping != null)
                {
                    mapping.UpdateMapping(item.ConceptCode, item.ConceptName, item.DefaultSeries);
                }
                else
                {
                    var newMapping = new InventoryConceptMapping(
                        request.BranchId,
                        item.MovementType,
                        item.Subtype,
                        item.ConceptCode,
                        string.IsNullOrWhiteSpace(item.ConceptName) ? item.SubtypeName : item.ConceptName,
                        item.DefaultSeries
                    );
                    _context.InventoryConceptMappings.Add(newMapping);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
