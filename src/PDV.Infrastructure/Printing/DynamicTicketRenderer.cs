using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PDV.Infrastructure.Printing;

public class DynamicTicketRenderer
{
    public static string Render(TicketTemplateJson template, Dictionary<string, string> variables, List<TicketTableItem> tableItems, int widthCharacters)
    {
        var sb = new StringBuilder();

        foreach (var block in template.Blocks)
        {
            switch (block.Type.ToLowerInvariant())
            {
                case "logo":
                    sb.AppendLine(Center("[LOGO]", widthCharacters));
                    break;

                case "text":
                    if (!string.IsNullOrEmpty(block.Content))
                    {
                        var resolvedText = ResolveVariables(block.Content, variables);
                        var lines = resolvedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            var formatted = FormatText(line, block.Align, block.Bold, block.FontSize, widthCharacters);
                            sb.AppendLine(formatted);
                        }
                    }
                    break;

                case "keyvalue":
                    if (!string.IsNullOrEmpty(block.Key))
                    {
                        var resolvedVal = !string.IsNullOrEmpty(block.ValuePlaceholder) 
                            ? ResolveVariables(block.ValuePlaceholder, variables) 
                            : string.Empty;
                        
                        sb.AppendLine(FormatKeyValue(block.Key, resolvedVal, block.Bold, widthCharacters));
                    }
                    break;

                case "separator":
                    var sepChar = string.IsNullOrEmpty(block.SeparatorChar) ? '-' : block.SeparatorChar[0];
                    sb.AppendLine(new string(sepChar, widthCharacters));
                    break;

                case "itemstable":
                    if (tableItems != null && tableItems.Count > 0 && block.Columns != null && block.Columns.Count > 0)
                    {
                        // Imprimir Encabezado de Tabla
                        var headerCols = new List<(string text, int width, bool alignRight)>();
                        foreach (var col in block.Columns)
                        {
                            int colW = (int)Math.Max(1, Math.Floor(widthCharacters * (col.WidthPercentage / 100.0)));
                            bool alignRight = col.Field.ToLowerInvariant() != "name" && col.Field.ToLowerInvariant() != "code";
                            headerCols.Add((col.Title.ToUpperInvariant(), colW, alignRight));
                        }
                        // Ajustar la última columna para llenar el ancho exacto
                        AdjustLastColumnWidth(headerCols, widthCharacters);
                        sb.AppendLine(FormatTableRow(widthCharacters, headerCols));
                        sb.AppendLine(new string('-', widthCharacters));

                        // Imprimir Filas de la Tabla
                        foreach (var item in tableItems)
                        {
                            var rowCols = new List<(string text, int width, bool alignRight)>();
                            foreach (var col in block.Columns)
                            {
                                int colW = (int)Math.Max(1, Math.Floor(widthCharacters * (col.WidthPercentage / 100.0)));
                                bool alignRight = col.Field.ToLowerInvariant() != "name" && col.Field.ToLowerInvariant() != "code";
                                
                                string textVal = col.Field.ToLowerInvariant() switch
                                {
                                    "name" => item.Name,
                                    "code" => item.Code,
                                    "quantity" => item.Quantity,
                                    "price" => item.Price,
                                    "total" => item.Total,
                                    _ => string.Empty
                                };

                                rowCols.Add((textVal, colW, alignRight));
                            }
                            AdjustLastColumnWidth(rowCols, widthCharacters);
                            sb.AppendLine(FormatTableRow(widthCharacters, rowCols));
                        }
                    }
                    break;

                case "totals":
                    RenderTotals(sb, variables, widthCharacters);
                    break;

                case "barcodeorqr":
                    if (!string.IsNullOrEmpty(block.CodifiedValue))
                    {
                        var value = ResolveVariables(block.CodifiedValue, variables);
                        if (block.CodeType?.ToLowerInvariant() == "qr")
                        {
                            sb.AppendLine($"[QR:{value}]");
                        }
                        else
                        {
                            sb.AppendLine($"[BARCODE:{value}]");
                        }
                    }
                    break;

                case "footer":
                    if (!string.IsNullOrEmpty(block.Content))
                    {
                        var resolvedText = ResolveVariables(block.Content, variables);
                        var lines = resolvedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            sb.AppendLine(Center(line.Trim(), widthCharacters));
                        }
                    }
                    break;
            }
        }

        return sb.ToString();
    }

    private static void AdjustLastColumnWidth(List<(string text, int width, bool alignRight)> cols, int totalWidth)
    {
        int sum = 0;
        for (int i = 0; i < cols.Count - 1; i++)
        {
            sum += cols[i].width;
        }
        if (cols.Count > 0)
        {
            var last = cols[cols.Count - 1];
            cols[cols.Count - 1] = (last.text, Math.Max(1, totalWidth - sum), last.alignRight);
        }
    }

    private static string ResolveVariables(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    private static string FormatText(string text, string? align, bool bold, string? fontSize, int width)
    {
        int adjustedWidth = width;
        bool isDoubleWidth = fontSize?.ToLowerInvariant() == "doublewidth" || fontSize?.ToLowerInvariant() == "large";
        if (isDoubleWidth)
        {
            adjustedWidth = width / 2;
        }

        var formatted = align?.ToLowerInvariant() switch
        {
            "center" => Center(text, adjustedWidth),
            "right" => text.PadLeft(adjustedWidth),
            _ => text.PadRight(adjustedWidth)
        };

        if (bold)
        {
            formatted = $"<B>{formatted}</B>";
        }

        if (fontSize?.ToLowerInvariant() == "doubleheight")
        {
            formatted = $"<DH>{formatted}</DH>";
        }
        else if (fontSize?.ToLowerInvariant() == "doublewidth")
        {
            formatted = $"<DW>{formatted}</DW>";
        }
        else if (fontSize?.ToLowerInvariant() == "large")
        {
            formatted = $"<LG>{formatted}</LG>";
        }

        return formatted;
    }

    private static string FormatKeyValue(string key, string value, bool bold, int width)
    {
        int valWidth = width - key.Length;
        if (valWidth < 5) valWidth = 5;
        
        string line = key + value.PadLeft(valWidth);
        if (line.Length > width)
        {
            line = line.Substring(0, width);
        }

        if (bold)
        {
            line = $"<B>{line}</B>";
        }
        return line;
    }

    private static string FormatTableRow(int width, List<(string text, int width, bool alignRight)> columns)
    {
        var sb = new StringBuilder();
        int currentPos = 0;

        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            string text = col.text ?? "";
            int colWidth = col.width;

            if (text.Length > colWidth)
            {
                if (i == 0 && colWidth > 3)
                {
                    text = text.Substring(0, colWidth - 3) + "...";
                }
                else
                {
                    text = text.Substring(0, colWidth);
                }
            }

            string padded = col.alignRight ? text.PadLeft(colWidth) : text.PadRight(colWidth);
            sb.Append(padded);
            currentPos += colWidth;
        }

        if (currentPos < width)
        {
            sb.Append(new string(' ', width - currentPos));
        }

        return sb.ToString();
    }

    private static void RenderTotals(StringBuilder sb, Dictionary<string, string> variables, int width)
    {
        variables.TryGetValue("{Subtotal}", out var subtotal);
        variables.TryGetValue("{Tax}", out var tax);
        variables.TryGetValue("{Total}", out var total);
        variables.TryGetValue("{PaymentMethod}", out var payMethod);

        if (!string.IsNullOrEmpty(subtotal))
        {
            sb.AppendLine(FormatKeyValue("Subtotal:", subtotal, false, width));
        }
        if (!string.IsNullOrEmpty(tax))
        {
            sb.AppendLine(FormatKeyValue("Impuestos:", tax, false, width));
        }
        if (!string.IsNullOrEmpty(total))
        {
            sb.AppendLine(new string('=', width));
            sb.AppendLine(FormatKeyValue("TOTAL:", total, true, width));
        }
        if (!string.IsNullOrEmpty(payMethod))
        {
            sb.AppendLine(FormatKeyValue("Método Pago:", payMethod, false, width));
        }
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text;
        int padding = (width - text.Length) / 2;
        return text.PadLeft(text.Length + padding).PadRight(width);
    }
}

public class TicketTableItem
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
}
