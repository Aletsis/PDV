using MediatR;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.TicketSequences.Commands.UpsertTicketSequences;

public record TicketSequenceUpsertDto(
    TicketSequenceType SequenceType,
    string? Series,
    int LastTicketNumber
);

public record UpsertTicketSequencesCommand(
    Guid CashRegisterId,
    List<TicketSequenceUpsertDto> Sequences
) : IRequest<Unit>;

public class UpsertTicketSequencesCommandHandler : IRequestHandler<UpsertTicketSequencesCommand, Unit>
{
    private readonly ITicketSequenceRepository _ticketSequenceRepository;

    public UpsertTicketSequencesCommandHandler(ITicketSequenceRepository ticketSequenceRepository)
    {
        _ticketSequenceRepository = ticketSequenceRepository;
    }

    public async Task<Unit> Handle(UpsertTicketSequencesCommand request, CancellationToken cancellationToken)
    {
        var existingEntities = await _ticketSequenceRepository.GetByCashRegisterAsync(request.CashRegisterId, cancellationToken);

        foreach (var seqDto in request.Sequences)
        {
            var existing = existingEntities.FirstOrDefault(e => e.SequenceType == seqDto.SequenceType);

            if (existing != null)
            {
                // Actualizar existente
                existing.UpdateSeries(seqDto.Series);
                existing.ResetTo(seqDto.LastTicketNumber);
                await _ticketSequenceRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                // Crear nueva secuencia
                var newEntity = new TicketSequence(request.CashRegisterId, seqDto.SequenceType, seqDto.Series);
                if (seqDto.LastTicketNumber > 0)
                {
                    newEntity.ResetTo(seqDto.LastTicketNumber);
                }
                await _ticketSequenceRepository.AddAsync(newEntity, cancellationToken);
            }
        }

        return Unit.Value;
    }
}
