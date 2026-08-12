using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Suppliers.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Domain.Enums;

namespace PDV.Infrastructure.Common;

public class ComercialApiSyncService : IComercialApiSyncService
{
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly ILogger<ComercialApiSyncService> _logger;

    public ComercialApiSyncService(
        ISystemConfigurationRepository systemConfigRepository,
        ILogger<ComercialApiSyncService> logger,
        ICurrentUserService? currentUserService = null)
    {
        _systemConfigRepository = systemConfigRepository;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    protected virtual async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken, string? usuario = null)
    {
        var config = await _systemConfigRepository.GetAsync(cancellationToken);
        if (config == null || string.IsNullOrWhiteSpace(config.ComercialApiUrl))
        {
            throw new InvalidOperationException("La URL de la API Comercial no está configurada.");
        }

        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri(config.ComercialApiUrl.TrimEnd('/') + "/");

        if (!string.IsNullOrWhiteSpace(config.ComercialApiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", config.ComercialApiKey);
        }

        var userToSend = !string.IsNullOrWhiteSpace(usuario) ? usuario : _currentUserService?.UserName;
        if (!string.IsNullOrWhiteSpace(userToSend))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Usuario", userToSend);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Name", userToSend);
        }

        return httpClient;
    }

    public async Task<bool> ProductExistsInComercialAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var endpoint = $"api/Productos?search={Uri.EscapeDataString(code)}&onlyActive=false";
            var response = await httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error al validar existencia del producto {Code} en Comercial. Código: {Status}", code, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ProductoDto>>(cancellationToken: cancellationToken);
            if (result == null || result.Items == null) return false;

            return result.Items.Any(i => i.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar existencia de producto {Code} en Comercial.", code);
            return false;
        }
    }

    public async Task<bool> SendProductToComercialAsync(Product product, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var code = product.Code.Trim();
            if (code.Length > 30) code = code.Substring(0, 30);

            var name = product.Name.Trim();
            if (name.Length > 60) name = name.Substring(0, 60);

            var unitId = (product.SaleUnitId.HasValue && product.SaleUnitId.Value > 0) ? product.SaleUnitId.Value : 1;
            var xmlUnitId = (product.XmlUnitId.HasValue && product.XmlUnitId.Value > 0) ? product.XmlUnitId.Value : (int?)null;

            var payload = new CreateProductoCommandDto
            {
                Codigo = code,
                Nombre = name,
                Descripcion = product.Description ?? "",
                TipoProducto = (int)product.Type,
                ControlExistencia = MapControlExistencia(product.ControlExistencia),
                IdUnidadBase = unitId,
                Precio1 = (double)product.Price,
                Precio2 = (double)(product.WholesalePrice ?? 0),
                Impuesto1 = MapTaxRate(product.TaxRate),
                Clasificacion1 = string.IsNullOrWhiteSpace(product.Department) ? null : product.Department.Trim(),
                Clasificacion5 = string.IsNullOrWhiteSpace(product.Category) ? null : product.Category.Trim(),
                CodigoSat = string.IsNullOrWhiteSpace(product.SatCode) ? null : product.SatCode.Trim(),
                IdUnidadXml = xmlUnitId,
                CodigoAlterno = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim()
            };

            var response = await httpClient.PostAsJsonAsync("api/Productos", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al registrar producto {Code} en Comercial. Código: {Status}, Detalle: {Body}", product.Code, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar producto {Code} a Comercial.", product.Code);
            return false;
        }
    }

    public async Task<bool> UpdateProductInComercialAsync(Product product, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var name = product.Name.Trim();
            if (name.Length > 60) name = name.Substring(0, 60);

            var unitId = (product.SaleUnitId.HasValue && product.SaleUnitId.Value > 0) ? product.SaleUnitId.Value : 1;
            var xmlUnitId = (product.XmlUnitId.HasValue && product.XmlUnitId.Value > 0) ? product.XmlUnitId.Value : (int?)null;

            var payload = new UpdateProductoCommandDto
            {
                Nombre = name,
                Descripcion = product.Description ?? "",
                TipoProducto = (int)product.Type,
                ControlExistencia = MapControlExistencia(product.ControlExistencia),
                Precio1 = (double)product.Price,
                Precio2 = (double)(product.WholesalePrice ?? 0),
                Impuesto1 = MapTaxRate(product.TaxRate),
                Clasificacion1 = string.IsNullOrWhiteSpace(product.Department) ? null : product.Department.Trim(),
                Clasificacion5 = string.IsNullOrWhiteSpace(product.Category) ? null : product.Category.Trim(),
                CodigoSat = string.IsNullOrWhiteSpace(product.SatCode) ? null : product.SatCode.Trim(),
                IdUnidadXml = xmlUnitId,
                IdUnidadBase = unitId,
                CodigoAlterno = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim()
            };

            var response = await httpClient.PutAsJsonAsync($"api/Productos/{Uri.EscapeDataString(product.Code.Trim())}", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al actualizar producto {Code} en Comercial. Código: {Status}, Detalle: {Body}", product.Code, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar producto {Code} en Comercial.", product.Code);
            return false;
        }
    }

    public async Task<bool> ClientExistsInComercialAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var endpoint = $"api/Clientes?search={Uri.EscapeDataString(code.Trim())}&onlyActive=false";
            var response = await httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error al validar existencia del cliente {Code} en Comercial. Código: {Status}", code, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ClienteDto>>(cancellationToken: cancellationToken);
            if (result == null || result.Items == null) return false;

            return result.Items.Any(i => (i.Codigo ?? "").Trim().Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar existencia de cliente {Code} en Comercial.", code);
            return false;
        }
    }

    public async Task<bool> SendClientToComercialAsync(Client client, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var rfc = string.IsNullOrWhiteSpace(client.TaxId) ? "XAXX010101000" : client.TaxId.Trim();
            var payload = new CreateClienteCommandDto
            {
                Codigo = client.Code.Trim(),
                RazonSocial = client.Name.Trim(),
                RFC = rfc,
                RegimenFiscal = client.FiscalRegime,
                UsoCFDI = client.CfdiUse
            };

            var response = await httpClient.PostAsJsonAsync("api/Clientes", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al registrar cliente {Code} en Comercial. Código: {Status}, Detalle: {Body}", client.Code, response.StatusCode, body);
                return false;
            }

            var commercialId = await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);

            if (client.Address != null && (!string.IsNullOrWhiteSpace(client.Address.Street) || !string.IsNullOrWhiteSpace(client.Address.Colony)))
            {
                var street = !string.IsNullOrWhiteSpace(client.Address.Street) ? client.Address.Street.Trim() : (!string.IsNullOrWhiteSpace(client.Address.Colony) ? client.Address.Colony.Trim() : "Conocido");
                var email = !string.IsNullOrWhiteSpace(client.Email) && client.Email.Contains('@') ? client.Email.Trim() : string.Empty;

                var addressPayload = new CreateDomicilioCommandDto
                {
                    CodigoCatalogo = client.Code.Trim(),
                    TipoCatalogo = 1,
                    TipoDireccion = 0, // Fiscal
                    Calle = street,
                    NumeroExterior = client.Address.ExteriorNumber ?? string.Empty,
                    NumeroInterior = client.Address.InteriorNumber ?? string.Empty,
                    Colonia = client.Address.Colony ?? string.Empty,
                    CodigoPostal = client.Address.ZipCode ?? string.Empty,
                    Ciudad = client.Address.City ?? string.Empty,
                    Estado = client.Address.State ?? string.Empty,
                    Pais = string.IsNullOrWhiteSpace(client.Address.Country) ? "México" : client.Address.Country,
                    Email = email,
                    Telefono1 = client.Phone ?? string.Empty
                };

                var addressResponse = await httpClient.PostAsJsonAsync("api/Domicilios", addressPayload, cancellationToken);
                if (!addressResponse.IsSuccessStatusCode)
                {
                    var addrBody = await addressResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Error al registrar domicilio para cliente {Code} en Comercial. Detalle: {Body}", client.Code, addrBody);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar cliente {Code} a Comercial.", client.Code);
            return false;
        }
    }

    public async Task<bool> UpdateClientInComercialAsync(Client client, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var rfc = string.IsNullOrWhiteSpace(client.TaxId) ? "XAXX010101000" : client.TaxId.Trim();
            var payload = new UpdateClienteCommandDto
            {
                RazonSocial = client.Name.Trim(),
                RFC = rfc,
                RegimenFiscal = client.FiscalRegime,
                UsoCFDI = client.CfdiUse
            };

            var response = await httpClient.PutAsJsonAsync($"api/Clientes/{Uri.EscapeDataString(client.Code.Trim())}", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al actualizar cliente {Code} en Comercial. Código: {Status}, Detalle: {Body}", client.Code, response.StatusCode, body);
                return false;
            }

            // Buscar ID comercial del cliente
            var searchUrl = $"api/Clientes?search={Uri.EscapeDataString(client.Code.Trim())}&onlyActive=false";
            var searchResponse = await httpClient.GetAsync(searchUrl, cancellationToken);
            if (!searchResponse.IsSuccessStatusCode) return true; // Si falla, al menos el cliente se actualizó

            var searchResult = await searchResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ClienteDto>>(cancellationToken: cancellationToken);
            var commercialClient = searchResult?.Items?.FirstOrDefault(i => (i.Codigo ?? "").Trim().Equals(client.Code.Trim(), StringComparison.OrdinalIgnoreCase));
            if (commercialClient == null) return true;

            int commercialId = commercialClient.Id;

            // Obtener domicilios del cliente en Comercial
            var addrUrl = $"api/Domicilios?catalogoId={commercialId}&tipoCatalogo=1";
            var addrResponse = await httpClient.GetAsync(addrUrl, cancellationToken);
            if (!addrResponse.IsSuccessStatusCode) return true;

            var addresses = await addrResponse.Content.ReadFromJsonAsync<List<DomicilioDto>>(cancellationToken: cancellationToken);
            var fiscalAddress = addresses?.FirstOrDefault(a => a.TipoDireccion == 0);

            if (client.Address != null && (!string.IsNullOrWhiteSpace(client.Address.Street) || !string.IsNullOrWhiteSpace(client.Address.Colony)))
            {
                var street = !string.IsNullOrWhiteSpace(client.Address.Street) ? client.Address.Street.Trim() : (!string.IsNullOrWhiteSpace(client.Address.Colony) ? client.Address.Colony.Trim() : "Conocido");
                var email = !string.IsNullOrWhiteSpace(client.Email) && client.Email.Contains('@') ? client.Email.Trim() : string.Empty;

                if (fiscalAddress != null)
                {
                    // Actualizar domicilio existente
                    var updateAddrPayload = new UpdateDomicilioCommandDto
                    {
                        Calle = street,
                        NumeroExterior = client.Address.ExteriorNumber ?? string.Empty,
                        NumeroInterior = client.Address.InteriorNumber ?? string.Empty,
                        Colonia = client.Address.Colony ?? string.Empty,
                        CodigoPostal = client.Address.ZipCode ?? string.Empty,
                        Ciudad = client.Address.City ?? string.Empty,
                        Estado = client.Address.State ?? string.Empty,
                        Pais = string.IsNullOrWhiteSpace(client.Address.Country) ? "México" : client.Address.Country,
                        Email = email,
                        Telefono1 = client.Phone ?? string.Empty
                    };

                    var putAddrResponse = await httpClient.PutAsJsonAsync($"api/Domicilios/{fiscalAddress.Id}", updateAddrPayload, cancellationToken);
                    if (!putAddrResponse.IsSuccessStatusCode)
                    {
                        var putBody = await putAddrResponse.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Error al actualizar domicilio {AddrId} para cliente {Code} en Comercial. Detalle: {Body}", fiscalAddress.Id, client.Code, putBody);
                    }
                }
                else
                {
                    // Crear nuevo domicilio
                    var createAddrPayload = new CreateDomicilioCommandDto
                    {
                        CodigoCatalogo = client.Code.Trim(),
                        TipoCatalogo = 1,
                        TipoDireccion = 0,
                        Calle = street,
                        NumeroExterior = client.Address.ExteriorNumber ?? string.Empty,
                        NumeroInterior = client.Address.InteriorNumber ?? string.Empty,
                        Colonia = client.Address.Colony ?? string.Empty,
                        CodigoPostal = client.Address.ZipCode ?? string.Empty,
                        Ciudad = client.Address.City ?? string.Empty,
                        Estado = client.Address.State ?? string.Empty,
                        Pais = string.IsNullOrWhiteSpace(client.Address.Country) ? "México" : client.Address.Country,
                        Email = email,
                        Telefono1 = client.Phone ?? string.Empty
                    };

                    var postAddrResponse = await httpClient.PostAsJsonAsync("api/Domicilios", createAddrPayload, cancellationToken);
                    if (!postAddrResponse.IsSuccessStatusCode)
                    {
                        var postBody = await postAddrResponse.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Error al registrar domicilio para cliente {Code} en Comercial. Detalle: {Body}", client.Code, postBody);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar cliente {Code} en Comercial.", client.Code);
            return false;
        }
    }

    public async Task<List<ComercialConceptoDto>> GetConceptosAsync(int tipoDocumento, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var endpoint = $"api/Conceptos?tipoDocumento={tipoDocumento}";
            var response = await httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error al obtener conceptos de Comercial para tipoDocumento {TipoDocumento}. Código: {Status}", tipoDocumento, response.StatusCode);
                return new List<ComercialConceptoDto>();
            }

            var result = await response.Content.ReadFromJsonAsync<List<ComercialConceptoDto>>(cancellationToken: cancellationToken);
            return result ?? new List<ComercialConceptoDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener conceptos de Comercial para tipoDocumento {TipoDocumento}.", tipoDocumento);
            return new List<ComercialConceptoDto>();
        }
    }

    public async Task<CreateFacturaResultDto?> GenerarFacturaComercialAsync(GenerarFacturaComercialDto command, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken, command.Usuario);
            var endpoint = "api/Facturas/generar";
            var response = await httpClient.PostAsJsonAsync(endpoint, command, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al generar factura en el API Comercial. Código: {Status}, Detalle: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Error al generar factura en Comercial: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<CreateFacturaResultDto>(cancellationToken: cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar factura en el API Comercial.");
            throw;
        }
    }

    public async Task<CreateFacturaResultDto?> GenerarFacturaGlobalComercialAsync(CreateFacturaGlobalCommandDto command, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken, command.Usuario);
            var endpoint = "api/Facturas/global";
            var response = await httpClient.PostAsJsonAsync(endpoint, command, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al generar factura global en el API Comercial. Código: {Status}, Detalle: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Error al generar factura global en Comercial: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<CreateFacturaResultDto>(cancellationToken: cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar factura global en el API Comercial.");
            throw;
        }
    }

    public async Task<bool> SendSupplierToComercialAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var rfc = string.IsNullOrWhiteSpace(supplier.TaxId) ? "XAXX010101000" : supplier.TaxId.Trim();
            var payload = new CreateClienteCommandDto
            {
                Codigo = supplier.Code.Trim(),
                RazonSocial = supplier.Name.Trim(),
                RFC = rfc,
                TipoCliente = 3 // Proveedor
            };

            var response = await httpClient.PostAsJsonAsync("api/Proveedores", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al registrar proveedor {Code} en Comercial. Código: {Status}, Detalle: {Body}", supplier.Code, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar proveedor {Code} a Comercial.", supplier.Code);
            return false;
        }
    }

    public async Task<List<SupplierDto>> GetSuppliersFromComercialAsync(string? search, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var endpoint = $"api/Proveedores?searchTerm={Uri.EscapeDataString(search ?? "")}&pageSize=100";
            var response = await httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error al consultar proveedores desde Comercial. Código: {Status}", response.StatusCode);
                return new List<SupplierDto>();
            }

            var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ClienteDto>>(cancellationToken: cancellationToken);
            if (result == null || result.Items == null) return new List<SupplierDto>();

            return result.Items.Select(c => new SupplierDto
            {
                Code = c.Codigo,
                Name = c.RazonSocial,
                TaxId = c.RFC ?? "",
                Email = c.Email ?? "",
                IsActive = c.Activo,
                CommercialId = c.Id
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proveedores desde Comercial.");
            return new List<SupplierDto>();
        }
    }

    public async Task<CreateDocumentoResultDto?> SendCompraToComercialAsync(SendCompraDto command, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken, command.Usuario);
            var endpoint = "api/Compras";
            var response = await httpClient.PostAsJsonAsync(endpoint, command, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al enviar Compra a Comercial. Código: {Status}, Detalle: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Error al registrar compra en Comercial: {body}");
            }

            return await response.Content.ReadFromJsonAsync<CreateDocumentoResultDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar Compra a Comercial.");
            throw;
        }
    }

    public async Task<CreateDocumentoResultDto?> SendEntradaToComercialAsync(SendEntradaDto command, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken, command.Usuario);
            var endpoint = "api/EntradasAlmacen";
            var response = await httpClient.PostAsJsonAsync(endpoint, command, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al enviar Entrada de Almacén a Comercial. Código: {Status}, Detalle: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Error al registrar entrada en Comercial: {body}");
            }

            var docId = await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
            return new CreateDocumentoResultDto
            {
                IdDocumento = docId,
                CodigoConcepto = command.CodigoConcepto,
                Serie = command.Serie,
                Folio = ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar Entrada de Almacén a Comercial.");
            throw;
        }
    }

    public async Task<CreateDocumentoResultDto?> SendSalidaToComercialAsync(SendSalidaDto command, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken, command.Usuario);
            var endpoint = "api/SalidasAlmacen";
            var response = await httpClient.PostAsJsonAsync(endpoint, command, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al enviar Salida de Almacén a Comercial. Código: {Status}, Detalle: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Error al registrar salida en Comercial: {body}");
            }

            return await response.Content.ReadFromJsonAsync<CreateDocumentoResultDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar Salida de Almacén a Comercial.");
            throw;
        }
    }

    public async Task<CreateDocumentoResultDto?> SendTraspasoToComercialAsync(SendTraspasoDto command, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken, command.Usuario);
            var endpoint = "api/SalidasAlmacen";

            var referencia = string.IsNullOrWhiteSpace(command.Referencia)
                ? (!string.IsNullOrWhiteSpace(command.CodigoAlmacenDestino) ? $"TRAS -> {command.CodigoAlmacenDestino}" : "")
                : command.Referencia;

            if (referencia.Length > 30)
            {
                referencia = referencia.Substring(0, 30);
            }

            var observaciones = command.Observaciones ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(command.CodigoAlmacenDestino) &&
                !observaciones.Contains(command.CodigoAlmacenDestino, StringComparison.OrdinalIgnoreCase))
            {
                var destNote = $"Destino: {command.CodigoAlmacenDestino}";
                observaciones = string.IsNullOrWhiteSpace(observaciones) ? destNote : $"{destNote}. {observaciones}";
            }

            var payload = new SendSalidaDto
            {
                CodigoConcepto = command.CodigoConcepto,
                Serie = command.Serie,
                Referencia = referencia,
                Observaciones = observaciones,
                Partidas = command.Partidas.Select(p => new SalidaPartidaSyncDto
                {
                    CodigoProducto = p.CodigoProducto,
                    CodigoAlmacen = command.CodigoAlmacenOrigen,
                    Unidades = p.Unidades
                }).ToList()
            };

            var response = await httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al enviar Traspaso a Comercial vía SalidasAlmacen. Código: {Status}, Detalle: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Error al registrar traspaso en Comercial: {body}");
            }

            return await response.Content.ReadFromJsonAsync<CreateDocumentoResultDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar Traspaso a Comercial vía SalidasAlmacen.");
            throw;
        }
    }

    private static int MapControlExistencia(ControlExistencia control)
    {
        return control switch
        {
            ControlExistencia.SinControl => 0,
            ControlExistencia.ConControl => 1,
            ControlExistencia.UnidadesDeMedidaYPeso => 1,
            ControlExistencia.Lotes => 2,
            ControlExistencia.Series => 3,
            ControlExistencia.Pedimentos => 4,
            _ => 1
        };
    }

    private static double MapTaxRate(TaxRateType rate)
    {
        return rate switch
        {
            TaxRateType.Rate16 => 16.0,
            TaxRateType.Rate8 => 8.0,
            _ => 0.0
        };
    }

    private class PaginatedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
    }

    private class ProductoDto
    {
        public string Codigo { get; set; } = string.Empty;
    }

    private class ClienteDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string? RFC { get; set; }
        public string? Email { get; set; }
        public bool Activo { get; set; }
    }

    private class DomicilioDto
    {
        public int Id { get; set; }
        public int CatalogoId { get; set; }
        public int TipoCatalogo { get; set; }
        public int TipoDireccion { get; set; }
        public string Calle { get; set; } = string.Empty;
        public string NumeroExterior { get; set; } = string.Empty;
        public string NumeroInterior { get; set; } = string.Empty;
        public string Colonia { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Pais { get; set; } = string.Empty;
        public string CodigoPostal { get; set; } = string.Empty;
    }

    private class CreateProductoCommandDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int TipoProducto { get; set; } = 1;
        public int ControlExistencia { get; set; } = 1;
        public int IdUnidadBase { get; set; } = 1;
        public double Precio1 { get; set; }
        public double Precio2 { get; set; }
        public double Impuesto1 { get; set; }
        public string? Clasificacion1 { get; set; }
        public string? Clasificacion5 { get; set; }
        public string? CodigoSat { get; set; }
        public int? IdUnidadXml { get; set; }
        public string? CodigoAlterno { get; set; }
    }

    private class UpdateProductoCommandDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int TipoProducto { get; set; } = 1;
        public int ControlExistencia { get; set; } = 1;
        public double Precio1 { get; set; }
        public double Precio2 { get; set; }
        public double Impuesto1 { get; set; }
        public string? Clasificacion1 { get; set; }
        public string? Clasificacion5 { get; set; }
        public string? CodigoSat { get; set; }
        public int? IdUnidadXml { get; set; }
        public int IdUnidadBase { get; set; } = 1;
        public string? CodigoAlterno { get; set; }
    }

    private class CreateClienteCommandDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string RFC { get; set; } = string.Empty;
        public string? RegimenFiscal { get; set; }
        public string? UsoCFDI { get; set; }
        public int TipoCliente { get; set; } = 1;
    }

    private class UpdateClienteCommandDto
    {
        public string RazonSocial { get; set; } = string.Empty;
        public string RFC { get; set; } = string.Empty;
        public string? RegimenFiscal { get; set; }
        public string? UsoCFDI { get; set; }
        public int TipoCliente { get; set; } = 1;
    }

    private class CreateDomicilioCommandDto
    {
        public string CodigoCatalogo { get; set; } = string.Empty;
        public int TipoCatalogo { get; set; } = 1;
        public int TipoDireccion { get; set; } = 0;
        public string Calle { get; set; } = string.Empty;
        public string NumeroExterior { get; set; } = string.Empty;
        public string NumeroInterior { get; set; } = string.Empty;
        public string Colonia { get; set; } = string.Empty;
        public string CodigoPostal { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Pais { get; set; } = "México";
        public string Telefono1 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    private class UpdateDomicilioCommandDto
    {
        public string Calle { get; set; } = string.Empty;
        public string NumeroExterior { get; set; } = string.Empty;
        public string NumeroInterior { get; set; } = string.Empty;
        public string Colonia { get; set; } = string.Empty;
        public string CodigoPostal { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Pais { get; set; } = "México";
        public string Telefono1 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
