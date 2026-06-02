using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.Logos.Queries.GetLogo;

public record GetLogoQuery(Guid Id) : IRequest<GetLogoResult?>;

public class GetLogoResult
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Base64 { get; set; } = string.Empty;
}

public class GetLogoQueryHandler : IRequestHandler<GetLogoQuery, GetLogoResult?>
{
    private readonly IApplicationDbContext _context;

    public GetLogoQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetLogoResult?> Handle(GetLogoQuery request, CancellationToken cancellationToken)
    {
        var e = await _context.Logos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (e == null) return null;
        return new GetLogoResult { Id = e.Id, FileName = e.FileName, ContentType = e.ContentType, Base64 = Convert.ToBase64String(e.Data) };
    }
}
