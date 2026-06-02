using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.FolioSequences.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.FolioSequences.Queries;

public record GetFolioSequencesByBranchQuery(Guid BranchId) : IRequest<List<FolioSequenceDto>>;

public class GetFolioSequencesByBranchQueryHandler : IRequestHandler<GetFolioSequencesByBranchQuery, List<FolioSequenceDto>>
{
    private readonly IApplicationDbContext _context;

    public GetFolioSequencesByBranchQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<FolioSequenceDto>> Handle(GetFolioSequencesByBranchQuery request, CancellationToken cancellationToken)
    {
        var existingEntities = await _context.FolioSequences
            .Where(f => f.BranchId == request.BranchId)
            .ToListAsync(cancellationToken);

        var result = new List<FolioSequenceDto>();

        foreach (InvoiceType type in Enum.GetValues(typeof(InvoiceType)))
        {
            var entity = existingEntities.FirstOrDefault(e => e.SeriesType == type);
            if (entity != null)
            {
                result.Add(new FolioSequenceDto
                {
                    Id = entity.Id,
                    BranchId = entity.BranchId,
                    SeriesType = entity.SeriesType,
                    Series = entity.Series,
                    LastFolio = entity.LastFolio,
                    FolioDigits = entity.FolioDigits,
                    ConceptCode = entity.ConceptCode
                });
            }
            else
            {
                // Retornar un DTO por defecto para que la UI pueda presentarlo
                result.Add(new FolioSequenceDto
                {
                    Id = Guid.Empty,
                    BranchId = request.BranchId,
                    SeriesType = type,
                    Series = GetDefaultSeries(type),
                    LastFolio = 0,
                    FolioDigits = 6,
                    ConceptCode = null
                });
            }
        }

        return result;
    }

    private string GetDefaultSeries(InvoiceType type)
    {
        return type switch
        {
            InvoiceType.Customer => "F",
            InvoiceType.Global => "FG",
            InvoiceType.CreditNote => "NC",
            _ => "SEC"
        };
    }
}
