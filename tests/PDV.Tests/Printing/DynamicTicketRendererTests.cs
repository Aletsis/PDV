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
}
