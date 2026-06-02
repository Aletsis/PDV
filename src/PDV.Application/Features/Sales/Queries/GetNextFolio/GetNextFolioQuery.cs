using MediatR;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Sales.Queries.GetNextFolio;

public record GetNextFolioQuery(Guid CashRegisterId) : IRequest<(string Series, int NextFolio)>;

public class GetNextFolioQueryHandler : IRequestHandler<GetNextFolioQuery, (string Series, int NextFolio)>
{
    private readonly ITicketSequenceRepository _ticketSequenceRepository;

    public GetNextFolioQueryHandler(ITicketSequenceRepository ticketSequenceRepository)
    {
        _ticketSequenceRepository = ticketSequenceRepository;
    }

    public async Task<(string Series, int NextFolio)> Handle(GetNextFolioQuery request, CancellationToken cancellationToken)
    {
        var sequence = await _ticketSequenceRepository.GetByRegisterAndTypeAsync(request.CashRegisterId, TicketSequenceType.Sale, cancellationToken);
        if (sequence == null)
        {
            return ("V", 1);
        }
        return (sequence.Series ?? "V", sequence.LastTicketNumber + 1);
    }
}
