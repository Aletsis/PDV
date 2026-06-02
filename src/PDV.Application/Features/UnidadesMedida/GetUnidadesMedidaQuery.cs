using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;

namespace PDV.Application.Features.UnidadesMedida;

public record GetUnidadesMedidaQuery : IRequest<List<UnidadMedidaDto>>;

public class GetUnidadesMedidaQueryHandler : IRequestHandler<GetUnidadesMedidaQuery, List<UnidadMedidaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUnidadesMedidaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UnidadMedidaDto>> Handle(GetUnidadesMedidaQuery request, CancellationToken cancellationToken)
    {
        return await _context.UnidadesMedida
            .Select(u => new UnidadMedidaDto
            {
                Id = u.Id,
                ExternalId = u.ExternalId,
                NombreUnidad = u.NombreUnidad,
                Abreviatura = u.Abreviatura,
                Despliegue = u.Despliegue,
                ClaveInt = u.ClaveInt,
                ClaveSat = u.ClaveSat
            })
            .ToListAsync(cancellationToken);
    }
}
