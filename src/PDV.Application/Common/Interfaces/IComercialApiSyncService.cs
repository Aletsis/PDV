using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PDV.Domain.Entities;
using PDV.Application.Features.Suppliers.Dtos;

namespace PDV.Application.Common.Interfaces;

public interface IComercialApiSyncService
{
    Task<bool> ProductExistsInComercialAsync(string code, CancellationToken cancellationToken);
    Task<bool> SendProductToComercialAsync(Product product, CancellationToken cancellationToken);
    Task<bool> UpdateProductInComercialAsync(Product product, CancellationToken cancellationToken);

    Task<bool> ClientExistsInComercialAsync(string code, CancellationToken cancellationToken);
    Task<bool> SendClientToComercialAsync(Client client, CancellationToken cancellationToken);
    Task<bool> UpdateClientInComercialAsync(Client client, CancellationToken cancellationToken);

    Task<bool> SendSupplierToComercialAsync(Supplier supplier, CancellationToken cancellationToken);
    Task<List<SupplierDto>> GetSuppliersFromComercialAsync(string? search, CancellationToken cancellationToken);

    Task<List<ComercialConceptoDto>> GetConceptosAsync(int tipoDocumento, CancellationToken cancellationToken);

    Task<CreateFacturaResultDto?> GenerarFacturaComercialAsync(GenerarFacturaComercialDto command, CancellationToken cancellationToken);
    Task<CreateFacturaResultDto?> GenerarFacturaGlobalComercialAsync(CreateFacturaGlobalCommandDto command, CancellationToken cancellationToken);

    Task<CreateDocumentoResultDto?> SendCompraToComercialAsync(SendCompraDto command, CancellationToken cancellationToken);
    Task<CreateDocumentoResultDto?> SendEntradaToComercialAsync(SendEntradaDto command, CancellationToken cancellationToken);
    Task<CreateDocumentoResultDto?> SendSalidaToComercialAsync(SendSalidaDto command, CancellationToken cancellationToken);
    Task<CreateDocumentoResultDto?> SendTraspasoToComercialAsync(SendTraspasoDto command, CancellationToken cancellationToken);
}

public class CreateDocumentoResultDto
{
    public int IdDocumento { get; set; }
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
}

public class SendCompraDto
{
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public double Folio { get; set; } = 0;
    public string CodigoProveedor { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public string CodigoAlmacen { get; set; } = string.Empty;
    public List<CompraPartidaSyncDto> Partidas { get; set; } = new();
}

public class CompraPartidaSyncDto
{
    public string CodigoProducto { get; set; } = string.Empty;
    public double Unidades { get; set; }
    public double PrecioUnitario { get; set; }
}

public class SendEntradaDto
{
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public List<EntradaPartidaSyncDto> Partidas { get; set; } = new();
}

public class EntradaPartidaSyncDto
{
    public string CodigoProducto { get; set; } = string.Empty;
    public string CodigoAlmacen { get; set; } = string.Empty;
    public double Unidades { get; set; }
    public double Costo { get; set; }
}

public class SendSalidaDto
{
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public List<SalidaPartidaSyncDto> Partidas { get; set; } = new();
}

public class SalidaPartidaSyncDto
{
    public string CodigoProducto { get; set; } = string.Empty;
    public string CodigoAlmacen { get; set; } = string.Empty;
    public double Unidades { get; set; }
}

public class SendTraspasoDto
{
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public double Folio { get; set; } = 0;
    public string CodigoAlmacenOrigen { get; set; } = string.Empty;
    public string CodigoAlmacenDestino { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public List<TraspasoPartidaSyncDto> Partidas { get; set; } = new();
}

public class TraspasoPartidaSyncDto
{
    public string CodigoProducto { get; set; } = string.Empty;
    public double Unidades { get; set; }
}

public class CreateFacturaGlobalCommandDto
{
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string CodigoClientePublicoGeneral { get; set; } = "PUBLICOGENERAL";
    public string Periodicidad { get; set; } = "01";
    public string Meses { get; set; } = "";
    public string Anio { get; set; } = "";
    public string UsoCfdi { get; set; } = "S01";
    public string MetodoPago { get; set; } = "PUE";
    public string FormaPago { get; set; } = "01";
    public string CodigoProductoGravado { get; set; } = string.Empty;
    public string CodigoProductoExento { get; set; } = string.Empty;
    public bool AutoTimbrar { get; set; } = true;
    public string CodigoAlmacen { get; set; } = "1";
    public List<ConceptoGlobalDto> Conceptos { get; set; } = new();
}

public class ConceptoGlobalDto
{
    public string NoIdentificacion { get; set; } = string.Empty;
    public double ValorUnitario { get; set; }
    public double Importe { get; set; }
    public List<TrasladoConceptoDto> Traslados { get; set; } = new();
}

public class TrasladoConceptoDto
{
    public double Base { get; set; }
    public string Impuesto { get; set; } = "002";
    public string TipoFactor { get; set; } = "Tasa";
    public string TasaOCuota { get; set; } = "0.160000";
    public double Importe { get; set; }
}

public class GenerarFacturaComercialDto
{
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string CodigoCliente { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string CodigoAgente { get; set; } = string.Empty;
    public int NumeroMoneda { get; set; } = 1;
    public double TipoCambio { get; set; } = 1.0;
    public string UsoCfdi { get; set; } = "G03";
    public string MetodoPago { get; set; } = "PUE";
    public string FormaPago { get; set; } = "01";
    public bool AutoTimbrar { get; set; } = true;
    public List<FacturaPartidaDto> Partidas { get; set; } = new();
}

public class FacturaPartidaDto
{
    public string CodigoProducto { get; set; } = string.Empty;
    public double Unidades { get; set; }
    public double PrecioUnitario { get; set; }
    public string CodigoAlmacen { get; set; } = "1";
}

public class CreateFacturaResultDto
{
    public int IdDocumento { get; set; }
    public string Serie { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public bool Timbrado { get; set; }
    public string? Mensaje { get; set; }
    public TimbradoResultDto? DatosFiscales { get; set; }
}

public class TimbradoResultDto
{
    public string UUID { get; set; } = string.Empty;
    public string CadenaOriginal { get; set; } = string.Empty;
    public string SelloDigitalEmisor { get; set; } = string.Empty;
    public string SelloDigitalSAT { get; set; } = string.Empty;
    public string NoCertificadoEmisor { get; set; } = string.Empty;
    public string NoCertificadoSAT { get; set; } = string.Empty;
    public string FechaTimbrado { get; set; } = string.Empty;
}
