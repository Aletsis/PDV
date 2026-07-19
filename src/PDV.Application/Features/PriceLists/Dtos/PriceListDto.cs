namespace PDV.Application.Features.PriceLists.Dtos;

public record PriceListDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive
);
