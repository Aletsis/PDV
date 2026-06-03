using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Printers.Queries.GetPrintersDelta;

public record GetPrintersDeltaQuery(DateTime SinceUtc) : IRequest<List<PrinterSyncDto>>;

public class PrinterSyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PrinterConnectionType ConnectionType { get; set; }
    public bool IsActive { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public string? DevicePath { get; set; }
    public int CodePage { get; set; }
    public int MaxWidth { get; set; }
    public Guid? BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetPrintersDeltaQueryHandler : IRequestHandler<GetPrintersDeltaQuery, List<PrinterSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPrintersDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PrinterSyncDto>> Handle(GetPrintersDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.Printers
            .IgnoreQueryFilters()
            .Where(p => p.CreatedAt > since || (p.LastModifiedAt != null && p.LastModifiedAt > since))
            .Select(p => new PrinterSyncDto
            {
                Id = p.Id,
                Name = p.Name,
                ConnectionType = p.ConnectionType,
                IsActive = p.IsActive,
                IpAddress = p.IpAddress,
                Port = p.Port,
                DevicePath = p.DevicePath,
                CodePage = p.CodePage,
                MaxWidth = p.MaxWidth,
                BranchId = p.BranchId,
                CreatedAt = p.CreatedAt,
                LastModifiedAt = p.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
