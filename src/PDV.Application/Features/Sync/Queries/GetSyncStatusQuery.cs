using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Sync.Queries;

public record GetSyncStatusQuery : IRequest<SyncStatusDto>;

public record SyncStatusDto(bool IsLocalMode, int PendingMessagesCount);

public class GetSyncStatusQueryHandler : IRequestHandler<GetSyncStatusQuery, SyncStatusDto>
{
    private readonly IApplicationDbContext _context;

    public GetSyncStatusQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SyncStatusDto> Handle(GetSyncStatusQuery request, CancellationToken cancellationToken)
    {
        // Detect mode by checking database provider
        bool isLocalMode = false;
        if (_context is DbContext dbContext)
        {
            isLocalMode = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        int pendingCount = 0;
        if (isLocalMode)
        {
            try
            {
                pendingCount = await _context.OutboxMessages
                    .CountAsync(m => m.State == Domain.Enums.OutboxState.Pending, cancellationToken);
            }
            catch (Exception)
            {
                pendingCount = 0;
            }
        }

        return new SyncStatusDto(isLocalMode, pendingCount);
    }
}
