using System.Collections.Generic;
using PDV.Infrastructure.Printing;
using Xunit;

namespace PDV.Tests.Printing;

public class DynamicTicketRendererTests
{
    [Fact]
    public void Render_ShouldResolveVariablesCorrectly()
    {
        // Arrange
        var template = new TicketTemplateJson
        {
            Blocks = new List<TicketBlock>
            {
                new() { Type = "Text", Content = "Empresa: {CompanyName}", Align = "Center" }
            }
        };

        var variables = new Dictionary<string, string>
        {
            { "{CompanyName}", "TIENDA TEST" }
        };

        // Act
        var result = DynamicTicketRenderer.Render(template, variables, new List<TicketTableItem>(), 48);

        // Assert
        Assert.Contains("Empresa: TIENDA TEST", result);
    }

    [Fact]
    public void Render_ShouldFormatTableWithPercentageWidths()
    {
        // Arrange
        var template = new TicketTemplateJson
        {
            Blocks = new List<TicketBlock>
            {
                new()
                {
                    Type = "ItemsTable",
                    Columns = new List<TableColumnOption>
                    {
                        new() { Field = "Name", Title = "Prod", WidthPercentage = 50 },
                        new() { Field = "Quantity", Title = "Cant", WidthPercentage = 25 },
                        new() { Field = "Total", Title = "Total", WidthPercentage = 25 }
                    }
                }
            }
        };

        var tableItems = new List<TicketTableItem>
        {
            new() { Name = "Coca Cola 600ml", Quantity = "2", Total = "$40.00" }
        };

        // Act
        var result = DynamicTicketRenderer.Render(template, new Dictionary<string, string>(), tableItems, 40);

        // Assert
        // Total chars is 40. 
        // Prod: 50% => 20 chars
        // Cant: 25% => 10 chars
        // Total: 25% => 10 chars
        Assert.Contains("PROD", result);
        Assert.Contains("CANT", result);
        Assert.Contains("TOTAL", result);
        Assert.Contains("Coca Cola 600ml", result);
    }

    [Fact]
    public void Render_ShouldFormatManifestOrdersAndTotalsConfigurableFields()
    {
        // Arrange
        var template = new TicketTemplateJson
        {
            Blocks = new List<TicketBlock>
            {
                new() 
                { 
                    Type = "ManifestOrders", 
                    ManifestOrderFields = new List<string> { "Folio", "Client", "Total" } 
                },
                new() 
                { 
                    Type = "ManifestTotals", 
                    ManifestTotalsFields = new List<string> { "CashTotal", "OrderCount", "CombinedTotal" } 
                }
            }
        };

        var variables = new Dictionary<string, string>
        {
            { "{ExpectedCash}", "$150.00" },
            { "{OrderCount}", "2" },
            { "{Total}", "$350.00" }
        };

        var manifestOrders = new List<ManifestOrderInfo>
        {
            new()
            {
                Folio = "SER-00123",
                Client = "Test Client",
                Address = "Test Address",
                Phone = "1234567890",
                Total = 150.00m,
                PaymentMethod = "Efectivo"
            }
        };

        // Act
        var result = DynamicTicketRenderer.Render(template, variables, new List<TicketTableItem>(), 40, manifestOrders);

        // Assert
        Assert.Contains("Pedido: SER-00123", result);
        Assert.Contains("Cliente: Test Client", result);
        Assert.Contains("Total:   $150.00 (Efectivo)", result);
        Assert.DoesNotContain("Test Address", result); // Configured fields omitted it

        Assert.Contains("Total Efectivo:", result);
        Assert.Contains("$150.00", result);
        Assert.Contains("Num. Pedidos:", result);
        Assert.Contains("TOTAL:", result);
        Assert.Contains("$350.00", result);
        Assert.DoesNotContain("Total Tarjeta", result); // Omitted
    }
}
