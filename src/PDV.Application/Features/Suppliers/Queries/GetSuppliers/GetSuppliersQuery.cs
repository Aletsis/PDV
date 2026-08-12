using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Suppliers.Dtos;

namespace PDV.Application.Features.Suppliers.Queries.GetSuppliers;

public record GetSuppliersQuery(string? SearchTerm = null, bool IncludeInactive = false) : IRequest<List<SupplierDto>>;

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, List<SupplierDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSuppliersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Suppliers.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(s => s.Code.ToLower().Contains(term) ||
                                     s.Name.ToLower().Contains(term) ||
                                     s.TaxId.ToLower().Contains(term) ||
                                     s.Phone.Contains(term));
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                TaxId = s.TaxId,
                Phone = s.Phone,
                Email = s.Email,
                Street = s.Address != null ? s.Address.Street : null,
                ExteriorNumber = s.Address != null ? s.Address.ExteriorNumber : null,
                InteriorNumber = s.Address != null ? s.Address.InteriorNumber : null,
                Colony = s.Address != null ? s.Address.Colony : null,
                City = s.Address != null ? s.Address.City : null,
                State = s.Address != null ? s.Address.State : null,
                ZipCode = s.Address != null ? s.Address.ZipCode : null,
                Country = s.Address != null ? s.Address.Country : null,
                IsActive = s.IsActive,
                CommercialId = s.CommercialId
            })
            .ToListAsync(cancellationToken);
    }
}
