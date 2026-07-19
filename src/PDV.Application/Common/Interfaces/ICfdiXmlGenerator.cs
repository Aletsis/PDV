using PDV.Domain.Entities;

namespace PDV.Application.Common.Interfaces;

public interface ICfdiXmlGenerator
{
    string GenerateCfdi40Xml(Invoice invoice, SystemConfiguration config, string metodoPago, string formaPago);
    string GenerateCadenaOriginal(string xml);
}
