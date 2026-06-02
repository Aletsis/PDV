using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Domain.Enums;

namespace PDV.Infrastructure.Common;

public class ComercialApiSyncService : IComercialApiSyncService
{
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ILogger<ComercialApiSyncService> _logger;

    public ComercialApiSyncService(
        ISystemConfigurationRepository systemConfigRepository,
        ILogger<ComercialApiSyncService> logger)
    {
        _systemConfigRepository = systemConfigRepository;
        _logger = logger;
    }

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
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
            var payload = new CreateProductoCommandDto
            {
                Codigo = product.Code,
                Nombre = product.Name,
                Descripcion = product.Description ?? "",
                TipoProducto = (int)product.Type,
                ControlExistencia = MapControlExistencia(product.ControlExistencia),
                IdUnidadBase = product.SaleUnitId ?? 1,
                Precio1 = (double)product.Price,
                Precio2 = (double)(product.WholesalePrice ?? 0),
                Impuesto1 = MapTaxRate(product.TaxRate),
                Clasificacion1 = string.IsNullOrWhiteSpace(product.Department) ? null : product.Department,
                Clasificacion5 = string.IsNullOrWhiteSpace(product.Category) ? null : product.Category,
                CodigoSat = string.IsNullOrWhiteSpace(product.SatCode) ? null : product.SatCode,
                IdUnidadXml = product.XmlUnitId,
                CodigoAlterno = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode
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
            var payload = new UpdateProductoCommandDto
            {
                Nombre = product.Name,
                Descripcion = product.Description ?? "",
                TipoProducto = (int)product.Type,
                ControlExistencia = MapControlExistencia(product.ControlExistencia),
                Precio1 = (double)product.Price,
                Precio2 = (double)(product.WholesalePrice ?? 0),
                Impuesto1 = MapTaxRate(product.TaxRate),
                Clasificacion1 = string.IsNullOrWhiteSpace(product.Department) ? null : product.Department,
                Clasificacion5 = string.IsNullOrWhiteSpace(product.Category) ? null : product.Category,
                CodigoSat = string.IsNullOrWhiteSpace(product.SatCode) ? null : product.SatCode,
                IdUnidadXml = product.XmlUnitId,
                IdUnidadBase = product.SaleUnitId ?? 1,
                CodigoAlterno = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode
            };

            var response = await httpClient.PutAsJsonAsync($"api/Productos/{Uri.EscapeDataString(product.Code)}", payload, cancellationToken);
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
            var endpoint = $"api/Clientes?search={Uri.EscapeDataString(code)}&onlyActive=false";
            var response = await httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error al validar existencia del cliente {Code} en Comercial. Código: {Status}", code, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ClienteDto>>(cancellationToken: cancellationToken);
            if (result == null || result.Items == null) return false;

            return result.Items.Any(i => i.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase));
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
            var payload = new CreateClienteCommandDto
            {
                Codigo = client.Code,
                RazonSocial = client.Name,
                RFC = client.TaxId
            };

            var response = await httpClient.PostAsJsonAsync("api/Clientes", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al registrar cliente {Code} en Comercial. Código: {Status}, Detalle: {Body}", client.Code, response.StatusCode, body);
                return false;
            }

            var commercialId = await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);

            if (client.Address != null && !string.IsNullOrWhiteSpace(client.Address.Street))
            {
                var addressPayload = new CreateDomicilioCommandDto
                {
                    CodigoCatalogo = client.Code,
                    TipoCatalogo = 1,
                    TipoDireccion = 0, // Fiscal
                    Calle = client.Address.Street,
                    CodigoPostal = client.Address.ZipCode,
                    Ciudad = client.Address.City,
                    Estado = client.Address.State,
                    Pais = client.Address.Country,
                    Email = client.Email,
                    Telefono1 = client.Phone
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
            var payload = new UpdateClienteCommandDto
            {
                RazonSocial = client.Name,
                RFC = client.TaxId
            };

            var response = await httpClient.PutAsJsonAsync($"api/Clientes/{Uri.EscapeDataString(client.Code)}", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error al actualizar cliente {Code} en Comercial. Código: {Status}, Detalle: {Body}", client.Code, response.StatusCode, body);
                return false;
            }

            // Buscar ID comercial del cliente
            var searchUrl = $"api/Clientes?search={Uri.EscapeDataString(client.Code)}&onlyActive=false";
            var searchResponse = await httpClient.GetAsync(searchUrl, cancellationToken);
            if (!searchResponse.IsSuccessStatusCode) return true; // Si falla, al menos el cliente se actualizó

            var searchResult = await searchResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ClienteDto>>(cancellationToken: cancellationToken);
            var commercialClient = searchResult?.Items?.FirstOrDefault(i => i.Codigo.Equals(client.Code, StringComparison.OrdinalIgnoreCase));
            if (commercialClient == null) return true;

            int commercialId = commercialClient.Id;

            // Obtener domicilios del cliente en Comercial
            var addrUrl = $"api/Domicilios?catalogoId={commercialId}&tipoCatalogo=1";
            var addrResponse = await httpClient.GetAsync(addrUrl, cancellationToken);
            if (!addrResponse.IsSuccessStatusCode) return true;

            var addresses = await addrResponse.Content.ReadFromJsonAsync<List<DomicilioDto>>(cancellationToken: cancellationToken);
            var fiscalAddress = addresses?.FirstOrDefault(a => a.TipoDireccion == 0);

            if (client.Address != null && !string.IsNullOrWhiteSpace(client.Address.Street))
            {
                if (fiscalAddress != null)
                {
                    // Actualizar domicilio existente
                    var updateAddrPayload = new UpdateDomicilioCommandDto
                    {
                        Calle = client.Address.Street,
                        CodigoPostal = client.Address.ZipCode,
                        Ciudad = client.Address.City,
                        Estado = client.Address.State,
                        Pais = client.Address.Country,
                        Email = client.Email,
                        Telefono1 = client.Phone
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
                        CodigoCatalogo = client.Code,
                        TipoCatalogo = 1,
                        TipoDireccion = 0,
                        Calle = client.Address.Street,
                        CodigoPostal = client.Address.ZipCode,
                        Ciudad = client.Address.City,
                        Estado = client.Address.State,
                        Pais = client.Address.Country,
                        Email = client.Email,
                        Telefono1 = client.Phone
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
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
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
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
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
        public int TipoCliente { get; set; } = 1;
    }

    private class UpdateClienteCommandDto
    {
        public string RazonSocial { get; set; } = string.Empty;
        public string RFC { get; set; } = string.Empty;
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
