using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;

namespace PDV.Infrastructure.Common;

public class CfdiXmlGenerator : ICfdiXmlGenerator
{
    private readonly IApplicationDbContext _context;

    public CfdiXmlGenerator(IApplicationDbContext context)
    {
        _context = context;
    }

    public string GenerateCfdi40Xml(Invoice invoice, SystemConfiguration config, string metodoPago, string formaPago)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));
        if (config == null) throw new ArgumentNullException(nameof(config));

        Sale? sale = null;
        Return? returnDoc = null;
        List<Sale> globalSales = new List<Sale>();

        if (invoice.Type == InvoiceType.Customer)
        {
            sale = invoice.Sale;
            if (sale == null && invoice.SaleId.HasValue)
            {
                sale = _context.Sales
                    .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefault(s => s.Id == invoice.SaleId.Value);
            }
            if (sale == null)
            {
                throw new InvalidOperationException($"Original sale not found for invoice {invoice.Id}");
            }
        }
        else if (invoice.Type == InvoiceType.CreditNote)
        {
            returnDoc = invoice.Return;
            if (returnDoc == null && invoice.ReturnId.HasValue)
            {
                returnDoc = _context.Returns
                    .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefault(r => r.Id == invoice.ReturnId.Value);
            }
            if (returnDoc == null)
            {
                throw new InvalidOperationException($"Original return not found for credit note {invoice.Id}");
            }
        }
        else if (invoice.Type == InvoiceType.Global)
        {
            globalSales = _context.Sales
                .Include(s => s.Items)
                .ThenInclude(i => i.Product)
                .Where(s => s.ShiftId == invoice.ShiftId && s.InvoiceId == invoice.Id.ToString())
                .ToList();

            if (!globalSales.Any())
            {
                globalSales = _context.Sales
                    .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                    .Where(s => s.ShiftId == invoice.ShiftId && s.IsPaid && !s.IsCancelled && (!s.IsInvoiced || s.InvoiceId == invoice.Id.ToString()))
                    .ToList();
            }
        }

        string lugarExpedicion = config.FiscalAddress?.ZipCode ?? "00000";
        if (lugarExpedicion == "00000" && invoice.Branch != null)
        {
            lugarExpedicion = invoice.Branch.Address?.ZipCode ?? "00000";
        }
        if (lugarExpedicion == "00000")
        {
            var branch = _context.Branches.FirstOrDefault(b => b.Id == invoice.BranchId);
            if (branch != null && !string.IsNullOrEmpty(branch.Address?.ZipCode))
            {
                lugarExpedicion = branch.Address.ZipCode;
            }
        }

        XNamespace cfdi = "http://www.sat.gob.mx/cfd/4";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        XElement? infoGlobalElement = null;
        if (invoice.Type == InvoiceType.Global)
        {
            infoGlobalElement = new XElement(cfdi + "InformacionGlobal",
                new XAttribute("Periodicidad", "01"), // Diario
                new XAttribute("Meses", invoice.InvoiceDate.Month.ToString("D2")),
                new XAttribute("Anio", invoice.InvoiceDate.Year.ToString())
            );
        }

        XElement? relacionadosElement = null;
        if (invoice.Type == InvoiceType.CreditNote && !string.IsNullOrEmpty(invoice.RelatedUuid))
        {
            relacionadosElement = new XElement(cfdi + "CfdiRelacionados",
                new XAttribute("TipoRelacion", invoice.RelationType ?? "01"),
                new XElement(cfdi + "CfdiRelacionado",
                    new XAttribute("UUID", invoice.RelatedUuid)
                )
            );
        }

        var conceptosElement = new XElement(cfdi + "Conceptos");

        if (invoice.Type == InvoiceType.Customer && sale != null)
        {
            foreach (var item in sale.Items)
            {
                string satCode = string.IsNullOrEmpty(item.Product?.SatCode) ? "01010101" : item.Product.SatCode;
                string claveUnidad = item.Product?.SaleType == SaleType.Piece ? "H87" : "KGM";
                string unidadName = item.Product?.SaleUnitName ?? (item.Product?.SaleType == SaleType.Piece ? "Pieza" : "Kilogramo");

                var concepto = new XElement(cfdi + "Concepto",
                    new XAttribute("ClaveProdServ", satCode),
                    new XAttribute("NoIdentificacion", item.Product?.Code ?? ""),
                    new XAttribute("Cantidad", item.Quantity.ToString("0.######", CultureInfo.InvariantCulture)),
                    new XAttribute("ClaveUnidad", claveUnidad),
                    new XAttribute("Unidad", unidadName),
                    new XAttribute("Descripcion", item.ProductName),
                    new XAttribute("ValorUnitario", item.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("Importe", item.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("ObjetoImp", "02")
                );

                if (!item.IsTaxExempt)
                {
                    concepto.Add(new XElement(cfdi + "Impuestos",
                        new XElement(cfdi + "Traslados",
                            new XElement(cfdi + "Traslado",
                                new XAttribute("Base", item.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                                new XAttribute("Impuesto", "002"),
                                new XAttribute("TipoFactor", "Tasa"),
                                new XAttribute("TasaOCuota", (item.TaxRate / 100m).ToString("0.000000", CultureInfo.InvariantCulture)),
                                new XAttribute("Importe", item.TotalTax.ToString("0.00", CultureInfo.InvariantCulture))
                            )
                        )
                    ));
                }
                conceptosElement.Add(concepto);
            }
        }
        else if (invoice.Type == InvoiceType.CreditNote && returnDoc != null)
        {
            foreach (var item in returnDoc.Items)
            {
                string satCode = string.IsNullOrEmpty(item.Product?.SatCode) ? "01010101" : item.Product.SatCode;
                string claveUnidad = item.Product?.SaleType == SaleType.Piece ? "H87" : "KGM";
                string unidadName = item.Product?.SaleUnitName ?? (item.Product?.SaleType == SaleType.Piece ? "Pieza" : "Kilogramo");

                var concepto = new XElement(cfdi + "Concepto",
                    new XAttribute("ClaveProdServ", satCode),
                    new XAttribute("NoIdentificacion", item.Product?.Code ?? ""),
                    new XAttribute("Cantidad", item.Quantity.ToString("0.######", CultureInfo.InvariantCulture)),
                    new XAttribute("ClaveUnidad", claveUnidad),
                    new XAttribute("Unidad", unidadName),
                    new XAttribute("Descripcion", item.ProductName),
                    new XAttribute("ValorUnitario", item.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("Importe", item.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("ObjetoImp", "02")
                );

                if (!item.IsTaxExempt)
                {
                    concepto.Add(new XElement(cfdi + "Impuestos",
                        new XElement(cfdi + "Traslados",
                            new XElement(cfdi + "Traslado",
                                new XAttribute("Base", item.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                                new XAttribute("Impuesto", "002"),
                                new XAttribute("TipoFactor", "Tasa"),
                                new XAttribute("TasaOCuota", (item.TaxRate / 100m).ToString("0.000000", CultureInfo.InvariantCulture)),
                                new XAttribute("Importe", item.TotalTax.ToString("0.00", CultureInfo.InvariantCulture))
                            )
                        )
                    ));
                }
                conceptosElement.Add(concepto);
            }
        }
        else if (invoice.Type == InvoiceType.Global)
        {
            foreach (var s in globalSales)
            {
                var concepto = new XElement(cfdi + "Concepto",
                    new XAttribute("ClaveProdServ", "01010101"),
                    new XAttribute("NoIdentificacion", s.SaleNumber),
                    new XAttribute("Cantidad", "1"),
                    new XAttribute("ClaveUnidad", "ACT"),
                    new XAttribute("Descripcion", "Venta"),
                    new XAttribute("ValorUnitario", s.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("Importe", s.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XAttribute("ObjetoImp", "02")
                );

                var traslados = s.Items
                    .GroupBy(i => new { i.TaxRate, i.IsTaxExempt })
                    .Select(g => new XElement(cfdi + "Traslado",
                        new XAttribute("Base", g.Sum(i => i.UnitPrice * i.Quantity).ToString("0.00", CultureInfo.InvariantCulture)),
                        new XAttribute("Impuesto", "002"),
                        new XAttribute("TipoFactor", g.Key.IsTaxExempt ? "Exento" : "Tasa"),
                        new XAttribute("TasaOCuota", g.Key.IsTaxExempt ? "0.000000" : (g.Key.TaxRate / 100m).ToString("0.000000", CultureInfo.InvariantCulture)),
                        new XAttribute("Importe", g.Key.IsTaxExempt ? "0.00" : g.Sum(i => (i.UnitPrice * i.Quantity) * (g.Key.TaxRate / 100m)).ToString("0.00", CultureInfo.InvariantCulture))
                    )).ToList();

                concepto.Add(new XElement(cfdi + "Impuestos",
                    new XElement(cfdi + "Traslados", traslados)
                ));

                conceptosElement.Add(concepto);
            }
        }

        var totalTrasladado = invoice.TaxBreakdowns.Sum(tb => tb.TaxAmount);
        var globalTrasladosElement = new XElement(cfdi + "Traslados");

        foreach (var tb in invoice.TaxBreakdowns)
        {
            globalTrasladosElement.Add(new XElement(cfdi + "Traslado",
                new XAttribute("Base", tb.BaseAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                new XAttribute("Impuesto", "002"),
                new XAttribute("TipoFactor", tb.IsExempt ? "Exento" : "Tasa"),
                new XAttribute("TasaOCuota", tb.IsExempt ? "0.000000" : (tb.Rate / 100m).ToString("0.000000", CultureInfo.InvariantCulture)),
                new XAttribute("Importe", tb.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture))
            ));
        }

        var impuestosElement = new XElement(cfdi + "Impuestos");
        if (totalTrasladado > 0)
        {
            impuestosElement.Add(new XAttribute("TotalImpuestosTrasladados", totalTrasladado.ToString("0.00", CultureInfo.InvariantCulture)));
        }
        impuestosElement.Add(globalTrasladosElement);

        var root = new XElement(cfdi + "Comprobante",
            new XAttribute(XNamespace.Xmlns + "cfdi", cfdi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
            new XAttribute(xsi + "schemaLocation", "http://www.sat.gob.mx/cfd/4 http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd"),
            new XAttribute("Version", "4.0"),
            new XAttribute("Serie", invoice.Series),
            new XAttribute("Folio", invoice.Folio),
            new XAttribute("Fecha", invoice.InvoiceDate.ToString("yyyy-MM-ddTHH:mm:ss")),
            new XAttribute("FormaPago", formaPago),
            new XAttribute("NoCertificado", config.CsdSerialNumber ?? ""),
            new XAttribute("Certificado", config.CsdCertificateData != null ? Convert.ToBase64String(config.CsdCertificateData) : ""),
            new XAttribute("SubTotal", invoice.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
            new XAttribute("Moneda", "MXN"),
            new XAttribute("Total", invoice.Total.ToString("0.00", CultureInfo.InvariantCulture)),
            new XAttribute("TipoDeComprobante", invoice.Type switch
            {
                InvoiceType.CreditNote => "E",
                _ => "I"
            }),
            new XAttribute("Exportacion", "01"),
            new XAttribute("MetodoPago", metodoPago),
            new XAttribute("LugarExpedicion", lugarExpedicion)
        );

        if (!string.IsNullOrEmpty(invoice.SelloDigitalEmisor))
        {
            root.Add(new XAttribute("Sello", invoice.SelloDigitalEmisor));
        }
        else
        {
            root.Add(new XAttribute("Sello", ""));
        }

        if (relacionadosElement != null) root.Add(relacionadosElement);
        if (infoGlobalElement != null) root.Add(infoGlobalElement);

        root.Add(new XElement(cfdi + "Emisor",
            new XAttribute("Rfc", config.TaxId),
            new XAttribute("Nombre", config.CompanyName),
            new XAttribute("RegimenFiscal", config.FiscalRegime)
        ));

        string receiverUsage = invoice.CfdiUsage switch
        {
            CfdiUsage.GeneralExpense => "G03",
            CfdiUsage.Acquisition => "I01",
            CfdiUsage.ToDefine => "S01",
            _ => "S01"
        };

        root.Add(new XElement(cfdi + "Receptor",
            new XAttribute("Rfc", invoice.ReceiverTaxId),
            new XAttribute("Nombre", invoice.ReceiverName),
            new XAttribute("DomicilioFiscalReceptor", invoice.ReceiverZipCode),
            new XAttribute("RegimenFiscalReceptor", invoice.ReceiverFiscalRegime),
            new XAttribute("UsoCFDI", receiverUsage)
        ));

        root.Add(conceptosElement);
        root.Add(impuestosElement);

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);

        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    public string GenerateCadenaOriginal(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root == null) return string.Empty;

        var sb = new StringBuilder();

        void AppendAttr(XElement el, string attrName)
        {
            var attr = el.Attribute(attrName);
            if (attr != null)
            {
                sb.Append(attr.Value.Trim() + "|");
            }
        }

        // Comprobante
        AppendAttr(root, "Version");
        AppendAttr(root, "Serie");
        AppendAttr(root, "Folio");
        AppendAttr(root, "Fecha");
        AppendAttr(root, "FormaPago");
        AppendAttr(root, "NoCertificado");
        AppendAttr(root, "CondicionesDePago");
        AppendAttr(root, "SubTotal");
        AppendAttr(root, "Descuento");
        AppendAttr(root, "Moneda");
        AppendAttr(root, "TipoCambio");
        AppendAttr(root, "Total");
        AppendAttr(root, "TipoDeComprobante");
        AppendAttr(root, "Exportacion");
        AppendAttr(root, "MetodoPago");
        AppendAttr(root, "LugarExpedicion");
        AppendAttr(root, "Confirmacion");

        XNamespace cfdi = "http://www.sat.gob.mx/cfd/4";
        
        // CfdiRelacionados
        var relacionados = root.Element(cfdi + "CfdiRelacionados");
        if (relacionados != null)
        {
            AppendAttr(relacionados, "TipoRelacion");
            foreach (var rel in relacionados.Elements(cfdi + "CfdiRelacionado"))
            {
                AppendAttr(rel, "UUID");
            }
        }

        // InformacionGlobal
        var infoGlobal = root.Element(cfdi + "InformacionGlobal");
        if (infoGlobal != null)
        {
            AppendAttr(infoGlobal, "Periodicidad");
            AppendAttr(infoGlobal, "Meses");
            AppendAttr(infoGlobal, "Anio");
        }

        // Emisor
        var emisor = root.Element(cfdi + "Emisor");
        if (emisor != null)
        {
            AppendAttr(emisor, "Rfc");
            AppendAttr(emisor, "Nombre");
            AppendAttr(emisor, "RegimenFiscal");
            AppendAttr(emisor, "FacAtrAdquirente");
        }

        // Receptor
        var receptor = root.Element(cfdi + "Receptor");
        if (receptor != null)
        {
            AppendAttr(receptor, "Rfc");
            AppendAttr(receptor, "Nombre");
            AppendAttr(receptor, "DomicilioFiscalReceptor");
            AppendAttr(receptor, "ResidenciaFiscal");
            AppendAttr(receptor, "NumRegIdTrib");
            AppendAttr(receptor, "RegimenFiscalReceptor");
            AppendAttr(receptor, "UsoCFDI");
        }

        // Conceptos
        var conceptos = root.Element(cfdi + "Conceptos");
        if (conceptos != null)
        {
            foreach (var concepto in conceptos.Elements(cfdi + "Concepto"))
            {
                AppendAttr(concepto, "ClaveProdServ");
                AppendAttr(concepto, "NoIdentificacion");
                AppendAttr(concepto, "Cantidad");
                AppendAttr(concepto, "ClaveUnidad");
                AppendAttr(concepto, "Unidad");
                AppendAttr(concepto, "Descripcion");
                AppendAttr(concepto, "ValorUnitario");
                AppendAttr(concepto, "Importe");
                AppendAttr(concepto, "Descuento");
                AppendAttr(concepto, "ObjetoImp");

                var imp = concepto.Element(cfdi + "Impuestos");
                if (imp != null)
                {
                    var traslados = imp.Element(cfdi + "Traslados");
                    if (traslados != null)
                    {
                        foreach (var t in traslados.Elements(cfdi + "Traslado"))
                        {
                            AppendAttr(t, "Base");
                            AppendAttr(t, "Impuesto");
                            AppendAttr(t, "TipoFactor");
                            AppendAttr(t, "TasaOCuota");
                            AppendAttr(t, "Importe");
                        }
                    }

                    var retenciones = imp.Element(cfdi + "Retenciones");
                    if (retenciones != null)
                    {
                        foreach (var r in retenciones.Elements(cfdi + "Retencion"))
                        {
                            AppendAttr(r, "Base");
                            AppendAttr(r, "Impuesto");
                            AppendAttr(r, "TipoFactor");
                            AppendAttr(r, "TasaOCuota");
                            AppendAttr(r, "Importe");
                        }
                    }
                }
            }
        }

        // Impuestos (Global)
        var globalImp = root.Element(cfdi + "Impuestos");
        if (globalImp != null)
        {
            var retenciones = globalImp.Element(cfdi + "Retenciones");
            if (retenciones != null)
            {
                foreach (var r in retenciones.Elements(cfdi + "Retencion"))
                {
                    AppendAttr(r, "Impuesto");
                    AppendAttr(r, "Importe");
                }
            }

            AppendAttr(globalImp, "TotalImpuestosRetenidos");

            var traslados = globalImp.Element(cfdi + "Traslados");
            if (traslados != null)
            {
                foreach (var t in traslados.Elements(cfdi + "Traslado"))
                {
                    AppendAttr(t, "Impuesto");
                    AppendAttr(t, "TipoFactor");
                    AppendAttr(t, "TasaOCuota");
                    AppendAttr(t, "Importe");
                }
            }

            AppendAttr(globalImp, "TotalImpuestosTrasladados");
        }

        var result = sb.ToString();
        // SAT specification normalization
        result = Regex.Replace(result, @"\s+", " ");
        
        return "||" + result + "|"; // Since result ended with |, appending | gives || at the end. Wait, actually, the last attribute appends a | so result already ends with |. We prepend ||, so we get ||attr1|attr2|...||
    }

    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
