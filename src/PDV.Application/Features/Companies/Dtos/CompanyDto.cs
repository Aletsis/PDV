using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Companies.Dtos;

public record CompanyDto(
    Guid Id,
    string Name,
    string RFC,
    Address? FiscalAddress,
    string Phone,
    string? Email,
    bool IsActive
);
