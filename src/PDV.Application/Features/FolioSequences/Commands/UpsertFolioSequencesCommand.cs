using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.FolioSequences.Commands;

public record FolioSequenceUpsertDto(
    InvoiceType SeriesType,
    string? ConceptCode,
    string Series,
    int LastFolio
);

public record UpsertFolioSequencesCommand(
    Guid BranchId,
    List<FolioSequenceUpsertDto> Sequences
) : IRequest<Unit>;

public class UpsertFolioSequencesCommandHandler : IRequestHandler<UpsertFolioSequencesCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpsertFolioSequencesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpsertFolioSequencesCommand request, CancellationToken cancellationToken)
    {
        var existingEntities = await _context.FolioSequences
            .Where(f => f.BranchId == request.BranchId)
            .ToListAsync(cancellationToken);

        foreach (var seqDto in request.Sequences)
        {
            var existing = existingEntities.FirstOrDefault(e => e.SeriesType == seqDto.SeriesType);

            if (existing != null)
            {
                // Actualizar existente
                existing.UpdateConcept(seqDto.ConceptCode, seqDto.Series, seqDto.LastFolio);
                _context.FolioSequences.Update(existing);
            }
            else
            {
                // Crear nueva secuencia
                var newEntity = new FolioSequence(request.BranchId, seqDto.SeriesType, seqDto.Series, 6);
                newEntity.UpdateConcept(seqDto.ConceptCode, seqDto.Series, seqDto.LastFolio);
                _context.FolioSequences.Add(newEntity);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
