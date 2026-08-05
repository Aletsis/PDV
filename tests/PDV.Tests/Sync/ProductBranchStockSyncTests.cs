using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Features.Products.Queries.GetProductBranchStocksDelta;
using PDV.Domain.Entities;
using PDV.Infrastructure.Persistence;
using Xunit;

namespace PDV.Tests.Sync;

public class ProductBranchStockSyncTests
{
    private DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_ProductBranchStock_Sync_Test_{Guid.NewGuid()}")
            .Options;
    }

    [Fact]
    public async Task Handle_GetProductBranchStocksDeltaQuery_ReturnsCorrectDeltas()
    {
        // Arrange
        var options = CreateNewContextOptions();
        await using var context = new AppDbContext(options);

        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var stock1 = new ProductBranchStock(productId1, branchId, 10, 2);
        var stock2 = new ProductBranchStock(productId2, branchId, 20, 5);

        context.ProductBranchStocks.AddRange(stock1, stock2);
        await context.SaveChangesAsync();

        var handler = new GetProductBranchStocksDeltaQueryHandler(context);

        // Act - Fetch with since = DateTime.MinValue (should return both)
        var queryAll = new GetProductBranchStocksDeltaQuery(DateTime.MinValue);
        var resultAll = await handler.Handle(queryAll, CancellationToken.None);

        // Assert
        Assert.NotNull(resultAll);
        Assert.Equal(2, resultAll.Count);

        var dto1 = resultAll.FirstOrDefault(x => x.ProductId == productId1);
        Assert.NotNull(dto1);
        Assert.Equal(10, dto1.Stock);
        Assert.Equal(2, dto1.MinStock);

        var dto2 = resultAll.FirstOrDefault(x => x.ProductId == productId2);
        Assert.NotNull(dto2);
        Assert.Equal(20, dto2.Stock);
        Assert.Equal(5, dto2.MinStock);
    }
}
