using MediatR;
using PDV.Application.Features.TicketSequences.Dtos;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.TicketSequences.Queries.GetTicketSequencesByCashRegister;

public record GetTicketSequencesByCashRegisterQuery(Guid CashRegisterId) : IRequest<List<TicketSequenceDto>>;

public class GetTicketSequencesByCashRegisterQueryHandler : IRequestHandler<GetTicketSequencesByCashRegisterQuery, List<TicketSequenceDto>>
{
    private readonly ITicketSequenceRepository _ticketSequenceRepository;

    public GetTicketSequencesByCashRegisterQueryHandler(ITicketSequenceRepository ticketSequenceRepository)
    {
        _ticketSequenceRepository = ticketSequenceRepository;
    }

    public async Task<List<TicketSequenceDto>> Handle(GetTicketSequencesByCashRegisterQuery request, CancellationToken cancellationToken)
    {
        var existingEntities = await _ticketSequenceRepository.GetByCashRegisterAsync(request.CashRegisterId, cancellationToken);
        var result = new List<TicketSequenceDto>();

        foreach (TicketSequenceType type in Enum.GetValues(typeof(TicketSequenceType)))
        {
            var entity = existingEntities.FirstOrDefault(e => e.SequenceType == type);
            if (entity != null)
            {
                result.Add(new TicketSequenceDto
                {
                    Id = entity.Id,
                    CashRegisterId = entity.CashRegisterId,
                    SequenceType = entity.SequenceType,
                    Series = entity.Series,
                    LastTicketNumber = entity.LastTicketNumber,
                    ResetOnNewShift = entity.ResetOnNewShift
                });
            }
            else
            {
                // Retornar un DTO por defecto para que la UI pueda presentarlo
                result.Add(new TicketSequenceDto
                {
                    Id = Guid.Empty,
                    CashRegisterId = request.CashRegisterId,
                    SequenceType = type,
                    Series = GetDefaultSeries(type),
                    LastTicketNumber = 0,
                    ResetOnNewShift = (type == TicketSequenceType.CashCollection)
                });
            }
        }

        return result;
    }

    private string GetDefaultSeries(TicketSequenceType type)
    {
        return type switch
        {
            TicketSequenceType.Sale => "V",
            TicketSequenceType.Return => "DEV",
            TicketSequenceType.Cancellation => "CAN",
            TicketSequenceType.CashCollection => "RET",
            TicketSequenceType.CashCut => "COR",
            TicketSequenceType.Order => "PED",
            _ => "SEC"
        };
    }
}
