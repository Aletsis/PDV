using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Printers.Dtos;

namespace PDV.Application.Features.Printers.Queries.GetPrinter;

public record GetPrinterQuery(Guid Id) : IRequest<PrinterDto?>;

public class GetPrinterQueryHandler : IRequestHandler<GetPrinterQuery, PrinterDto?>
{
    private readonly IApplicationDbContext _context;

    public GetPrinterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PrinterDto?> Handle(GetPrinterQuery request, CancellationToken cancellationToken)
    {
        var e = await _context.Printers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (e == null) return null;
        return new PrinterDto { Id = e.Id, Name = e.Name, IpAddress = e.IpAddress, Port = e.Port, CodePage = e.CodePage, MaxWidth = e.MaxWidth, IsActive = e.IsActive };
    }
}
