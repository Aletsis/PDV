namespace PDV.Application.Features.PriceLists.Dtos;

public record PriceListProductDto(
    Guid PriceListId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    decimal OriginalPrice,
    decimal CustomPrice,
    bool IsOverridden
);
