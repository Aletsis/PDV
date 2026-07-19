using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PDV.Application.Common.Interfaces;

namespace PDV.Infrastructure.Common;

public class MockPacService : IPacService
{
    public async Task<PacStampResult> StampXmlAsync(string xml, string apiUser, string apiKey, string pacUrl, CancellationToken cancellationToken)
    {
        // Simulate network delay
        await Task.Delay(200, cancellationToken);

        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null)
            {
                return new PacStampResult(false, "Invalid XML structure.", null, null, null, null, null, null);
            }

            XNamespace cfdi = "http://www.sat.gob.mx/cfd/4";
            XNamespace tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            // Extract the Sello from the emisor
            string selloCfd = root.Attribute("Sello")?.Value ?? string.Empty;

            // Generate UUID, stamp date, and mock SAT info
            string uuid = Guid.NewGuid().ToString().ToUpperInvariant();
            DateTime stampedAt = DateTime.UtcNow;
            string stampedAtStr = stampedAt.ToString("yyyy-MM-ddTHH:mm:ss");
            string satCertNumber = "00001000000505142236";
            string rfcProvCertif = "MAS0810247C0"; // SAT provider RFC
            
            // Mock SAT signature
            string selloSat = Convert.ToBase64String(Encoding.UTF8.GetBytes($"SelloSAT_{uuid}_{selloCfd.Substring(0, Math.Min(10, selloCfd.Length))}"));

            // Build Timbre Fiscal Digital
            var tfdElement = new XElement(tfd + "TimbreFiscalDigital",
                new XAttribute(XNamespace.Xmlns + "tfd", tfd.NamespaceName),
                new XAttribute(xsi + "schemaLocation", "http://www.sat.gob.mx/TimbreFiscalDigital http://www.sat.gob.mx/sitio_internet/cfd/TimbreFiscalDigital/TimbreFiscalDigitalv11.xsd"),
                new XAttribute("Version", "1.1"),
                new XAttribute("UUID", uuid),
                new XAttribute("FechaTimbrado", stampedAtStr),
                new XAttribute("RfcProvCertif", rfcProvCertif),
                new XAttribute("SelloCFD", selloCfd),
                new XAttribute("NoCertificadoSAT", satCertNumber),
                new XAttribute("SelloSAT", selloSat)
            );

            // Add Complemento
            var complemento = root.Element(cfdi + "Complemento");
            if (complemento == null)
            {
                complemento = new XElement(cfdi + "Complemento");
                root.Add(complemento);
            }
            complemento.Add(tfdElement);

            // Generate TFD Cadena Original (for display/testing)
            string cadenaOriginalTfd = $"||1.1|{uuid}|{stampedAtStr}|{rfcProvCertif}|{selloCfd}|{satCertNumber}||";

            // Save modified document
            using var sw = new Utf8StringWriter();
            doc.Save(sw);
            string stampedXml = sw.ToString();

            return new PacStampResult(
                Success: true,
                ErrorMessage: null,
                StampedXml: stampedXml,
                Uuid: uuid,
                StampedAt: stampedAt,
                SelloSAT: selloSat,
                CertificadoSAT: satCertNumber,
                CadenaOriginalTfd: cadenaOriginalTfd
            );
        }
        catch (Exception ex)
        {
            return new PacStampResult(
                Success: false,
                ErrorMessage: $"Error parsing/signing XML: {ex.Message}",
                StampedXml: null,
                Uuid: null,
                StampedAt: null,
                SelloSAT: null,
                CertificadoSAT: null,
                CadenaOriginalTfd: null
            );
        }
    }

    public async Task<PacCancelResult> CancelInvoiceAsync(
        string uuid, 
        string rfcEmisor, 
        string rfcReceptor, 
        decimal total, 
        string motivo, 
        string? uuidSustituto, 
        string apiUser, 
        string apiKey, 
        string pacUrl, 
        CancellationToken cancellationToken)
    {
        // Simulate network delay
        await Task.Delay(200, cancellationToken);

        DateTime cancelledAt = DateTime.UtcNow;
        string cancelledAtStr = cancelledAt.ToString("yyyy-MM-ddTHH:mm:ss");

        // Mock acuse XML
        string acuseXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Acuse xmlns=""http://www.sat.gob.mx/cancelacion"" Fecha=""{cancelledAtStr}"" RfcEmisor=""{rfcEmisor}"">
  <Folios>
    <UUID>{uuid}</UUID>
    <EstatusUUID>201</EstatusUUID>
  </Folios>
  <Signature xmlns=""http://www.w3.org/2000/09/xmldsig#"">
    <SignatureValue>MOCK_SIGNATURE_FOR_{uuid}</SignatureValue>
  </Signature>
</Acuse>";

        return new PacCancelResult(
            Success: true,
            ErrorMessage: null,
            AcuseXml: acuseXml,
            CancelledAt: cancelledAt
        );
    }

    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
