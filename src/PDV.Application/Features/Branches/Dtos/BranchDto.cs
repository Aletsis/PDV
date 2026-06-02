using PDV.Domain.ValueObjects;

namespace PDV.Application.Features.Branches.Dtos;

public record BranchDto(
    Guid Id,
    string Name,
    string Code,
    Address? Address,
    string Phone,
    string? Email,
    bool IsActive,
    bool IsMainBranch
);
