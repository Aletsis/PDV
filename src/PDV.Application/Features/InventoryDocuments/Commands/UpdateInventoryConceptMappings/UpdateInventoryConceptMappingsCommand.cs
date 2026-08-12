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

namespace PDV.Application.Features.InventoryDocuments.Commands.UpdateInventoryConceptMappings;

public record UpdateInventoryConceptMappingsCommand(List<InventoryConceptMappingDto> Mappings) : IRequest<bool>;

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

        var currentMappings = await _context.InventoryConceptMappings.ToListAsync(cancellationToken);

        foreach (var item in request.Mappings)
        {
            var mapping = currentMappings.FirstOrDefault(m => m.Subtype == item.Subtype);
            if (mapping != null)
            {
                mapping.UpdateMapping(item.ConceptCode, item.ConceptName, item.DefaultSeries);
            }
            else
            {
                var newMapping = new InventoryConceptMapping(item.Subtype, item.ConceptCode, item.ConceptName, item.DefaultSeries);
                _context.InventoryConceptMappings.Add(newMapping);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
