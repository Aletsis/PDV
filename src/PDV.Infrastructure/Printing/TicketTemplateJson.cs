using System.Collections.Generic;

namespace PDV.Infrastructure.Printing;

public class TicketTemplateJson
{
    public List<TicketBlock> Blocks { get; set; } = new();
}

public class TicketBlock
{
    public string Type { get; set; } = string.Empty; // "Logo", "Text", "KeyValue", "Separator", "ItemsTable", "Totals", "BarcodeOrQr", "Footer"
    
    // Propiedades para Text y KeyValue
    public string? Content { get; set; }
    public string? Align { get; set; } // "Left", "Center", "Right"
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string? FontSize { get; set; } // "Normal", "DoubleHeight", "DoubleWidth", "Large"
    
    // Propiedades para KeyValue
    public string? Key { get; set; }
    public string? ValuePlaceholder { get; set; }
    
    // Propiedades para Separator
    public string? SeparatorChar { get; set; } // "-", "=", "*", etc.
    
    // Propiedades para Logo
    public string? LogoScale { get; set; } // "25", "50", "100"
    
    // Propiedades para Tabla
    public List<TableColumnOption>? Columns { get; set; }
    public bool WrapText { get; set; }
    
    // Propiedades para BarcodeOrQr
    public string? CodeType { get; set; } // "QR", "Barcode"
    public string? CodifiedValue { get; set; } // placeholder (e.g. "{SaleNumber}")
    public int ModuleSize { get; set; } = 4;

    // Propiedades para Totals
    public List<string>? TotalsFields { get; set; } = new();

    // Propiedades para ManifestOrders y ManifestTotals
    public List<string>? ManifestOrderFields { get; set; } = new();
    public List<string>? ManifestTotalsFields { get; set; } = new();
}

public class TableColumnOption
{
    public string Field { get; set; } = string.Empty; // "Name", "Quantity", "Price", "Total", "Code"
    public string Title { get; set; } = string.Empty;
    public double WidthPercentage { get; set; }
}
