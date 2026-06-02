using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Printers.Dtos;

namespace PDV.Application.Features.Printers.Queries.ListPrinters;

public record ListPrintersQuery : IRequest<List<PrinterDto>>;

public class ListPrintersQueryHandler : IRequestHandler<ListPrintersQuery, List<PrinterDto>>
{
    private readonly IApplicationDbContext _context;

    public ListPrintersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PrinterDto>> Handle(ListPrintersQuery request, CancellationToken cancellationToken)
    {
        return await _context.Printers.AsNoTracking()
            .Select(e => new PrinterDto { Id = e.Id, Name = e.Name, IpAddress = e.IpAddress, Port = e.Port, CodePage = e.CodePage, MaxWidth = e.MaxWidth, IsActive = e.IsActive })
            .ToListAsync(cancellationToken);
    }
}
