using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;

namespace PDV.Application.Features.Logos.Commands.CreateLogo;

public record CreateLogoCommand(byte[] Data, string FileName, string ContentType) : IRequest<Guid>;

public class CreateLogoCommandHandler : IRequestHandler<CreateLogoCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateLogoCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateLogoCommand request, CancellationToken cancellationToken)
    {
        var entity = new Logo(
            request.FileName,
            request.ContentType,
            request.Data,
            PDV.Domain.Enums.LogoPurpose.Ticket
        );

        _context.Logos.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
