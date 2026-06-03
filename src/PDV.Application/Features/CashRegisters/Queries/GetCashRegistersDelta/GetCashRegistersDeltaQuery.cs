using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.CashRegisters.Queries.GetCashRegistersDelta;

public record GetCashRegistersDeltaQuery(DateTime SinceUtc) : IRequest<List<CashRegisterSyncDto>>;

public class CashRegisterSyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? IpAddress { get; set; }
    public int Mode { get; set; }
    public Guid BranchId { get; set; }
    public Guid? AssignedEmployeeId { get; set; }
    public Guid? AssignedPrinterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class GetCashRegistersDeltaQueryHandler : IRequestHandler<GetCashRegistersDeltaQuery, List<CashRegisterSyncDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCashRegistersDeltaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CashRegisterSyncDto>> Handle(GetCashRegistersDeltaQuery request, CancellationToken cancellationToken)
    {
        var since = request.SinceUtc;

        return await _context.CashRegisters
            .IgnoreQueryFilters()
            .Where(c => c.CreatedAt > since || (c.LastModifiedAt != null && c.LastModifiedAt > since))
            .Select(c => new CashRegisterSyncDto
            {
                Id = c.Id,
                Name = c.Name,
                Location = c.Location,
                IsActive = c.IsActive,
                IpAddress = c.IpAddress,
                Mode = (int)c.Mode,
                BranchId = c.BranchId,
                AssignedEmployeeId = c.AssignedEmployeeId,
                AssignedPrinterId = c.AssignedPrinterId,
                CreatedAt = c.CreatedAt,
                LastModifiedAt = c.LastModifiedAt
            })
            .ToListAsync(cancellationToken);
    }
}
