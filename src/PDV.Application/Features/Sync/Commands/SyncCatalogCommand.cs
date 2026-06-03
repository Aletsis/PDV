using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;

namespace PDV.Application.Features.Sync.Commands;

public enum CatalogType
{
    UnidadesMedida = 1,
    Departamentos = 2,
    Categorias = 3,
    Productos = 4,
    Clientes = 5
}

public class SyncResult
{
    public bool Success { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public record SyncCatalogCommand(CatalogType Catalog, IProgress<string> Progress) : IRequest<SyncResult>;

public class SyncCatalogCommandHandler : IRequestHandler<SyncCatalogCommand, SyncResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISystemConfigurationRepository _systemConfigRepository;

    public SyncCatalogCommandHandler(IApplicationDbContext context, ISystemConfigurationRepository systemConfigRepository)
    {
        _context = context;
        _systemConfigRepository = systemConfigRepository;
    }

    public async Task<SyncResult> Handle(SyncCatalogCommand request, CancellationToken cancellationToken)
    {
        var result = new SyncResult { Success = false };
        request.Progress.Report($"Iniciando proceso de sincronización para {request.Catalog}...");

        try
        {
            request.Progress.Report("Obteniendo configuración del sistema mediante el repositorio...");
            var config = await _systemConfigRepository.GetAsync(cancellationToken);

            if (config == null || string.IsNullOrWhiteSpace(config.ComercialApiUrl))
            {
                var errMsg = "La URL de la API Comercial no está configurada en los ajustes del sistema.";
                request.Progress.Report($"ERROR: {errMsg}");
                result.ErrorMessage = errMsg;
                return result;
            }

            if (request.Catalog == CatalogType.Productos)
            {
                await SyncProductosCatalogAsync(config, result, request.Progress, cancellationToken);
            }
            else if (request.Catalog == CatalogType.Clientes)
            {
                await SyncClientesCatalogAsync(config, result, request.Progress, cancellationToken);
            }
            else
            {
                // Seleccionar endpoint según el catálogo
                string endpoint;
                if (request.Catalog == CatalogType.UnidadesMedida)
                {
                    endpoint = $"{config.ComercialApiUrl.TrimEnd('/')}/api/UnidadesMedida";
                }
                else if (request.Catalog == CatalogType.Departamentos)
                {
                    endpoint = $"{config.ComercialApiUrl.TrimEnd('/')}/api/Clasificaciones/valores?clasificacionId=25";
                }
                else // Categorias
                {
                    endpoint = $"{config.ComercialApiUrl.TrimEnd('/')}/api/Clasificaciones/valores?clasificacionId=29";
                }

                request.Progress.Report($"Conectando con el servidor de sincronización: {endpoint}");

                using var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                using var httpClient = new HttpClient(handler);
                if (!string.IsNullOrWhiteSpace(config.ComercialApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Api-Key", config.ComercialApiKey);
                    request.Progress.Report("API Key configurada y añadida a los encabezados de la petición.");
                }

                var response = await httpClient.GetAsync(endpoint, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var errMsg = $"El servidor respondió con código de error {response.StatusCode}. Detalle: {errorBody}";
                    request.Progress.Report($"ERROR: {errMsg}");
                    result.ErrorMessage = errMsg;
                    return result;
                }

                if (request.Catalog == CatalogType.UnidadesMedida)
                {
                    var units = await response.Content.ReadFromJsonAsync<List<UnidadMedidaSyncDto>>(cancellationToken: cancellationToken);
                    if (units == null || !units.Any())
                    {
                        request.Progress.Report("Sincronización finalizada: No se recibieron unidades de medida.");
                        result.Success = true;
                        return result;
                    }

                    request.Progress.Report($"Se recibieron {units.Count} unidades de medida. Iniciando persistencia...");
                    await SyncUnidadesMedidaAsync(units, result, request.Progress, cancellationToken);
                }
                else
                {
                    var values = await response.Content.ReadFromJsonAsync<List<ClasificacionValorSyncDto>>(cancellationToken: cancellationToken);
                    if (values == null || !values.Any())
                    {
                        request.Progress.Report($"Sincronización finalizada: No se recibieron registros para {request.Catalog}.");
                        result.Success = true;
                        return result;
                    }

                    request.Progress.Report($"Se recibieron {values.Count} registros de clasificación. Iniciando persistencia...");
                    if (request.Catalog == CatalogType.Departamentos)
                    {
                        await SyncDepartamentosAsync(values, result, request.Progress, cancellationToken);
                    }
                    else
                    {
                        await SyncCategoriasAsync(values, result, request.Progress, cancellationToken);
                    }
                }
            }

            request.Progress.Report("Guardando todos los cambios persistidos en la base de datos...");
            await _context.SaveChangesAsync(cancellationToken);

            result.Success = true;
            request.Progress.Report($"¡Sincronización de {request.Catalog} completada con éxito!");
            request.Progress.Report($"Registros Nuevos: {result.CreatedCount}, Actualizados: {result.UpdatedCount}, Omitidos por error: {result.SkippedCount}");
        }
        catch (Exception ex)
        {
            var errMsg = $"ERROR CRÍTICO: {ex.Message}";
            request.Progress.Report(errMsg);
            if (ex.InnerException != null)
            {
                request.Progress.Report($"DETALLE SSL/RED: {ex.InnerException.Message}");
            }
            request.Progress.Report(ex.StackTrace ?? string.Empty);
            result.ErrorMessage = ex.Message;
        }
        return result;
    }

    private async Task SyncUnidadesMedidaAsync(List<UnidadMedidaSyncDto> dtos, SyncResult result, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var ids = dtos.Select(d => d.Id).ToList();
        var existingDict = await _context.UnidadesMedida
            .Where(u => ids.Contains(u.ExternalId))
            .ToDictionaryAsync(u => u.ExternalId, u => u, cancellationToken);

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.NombreUnidad))
            {
                result.SkippedCount++;
                progress.Report($"[OMITIDO] Registro ID {dto.Id} omitido porque el nombre de la unidad de medida está vacío.");
                continue;
            }

            if (!existingDict.TryGetValue(dto.Id, out var existing))
            {
                var unit = new UnidadMedida(
                    externalId: dto.Id,
                    nombreUnidad: dto.NombreUnidad,
                    abreviatura: dto.Abreviatura,
                    despliegue: dto.Despliegue,
                    claveInt: dto.ClaveInt,
                    claveSat: dto.ClaveSat
                );
                unit.ClearDomainEvents();
                _context.UnidadesMedida.Add(unit);
                result.CreatedCount++;
                progress.Report($"[NUEVO] Unidad '{dto.NombreUnidad}' agregada.");
            }
            else
            {
                existing.Update(
                    nombreUnidad: dto.NombreUnidad,
                    abreviatura: dto.Abreviatura,
                    despliegue: dto.Despliegue,
                    claveInt: dto.ClaveInt,
                    claveSat: dto.ClaveSat
                );
                existing.ClearDomainEvents();
                result.UpdatedCount++;
                progress.Report($"[ACTUALIZADO] Unidad '{dto.NombreUnidad}' actualizada.");
            }
        }
    }

    private async Task SyncDepartamentosAsync(List<ClasificacionValorSyncDto> dtos, SyncResult result, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var ids = dtos.Select(d => d.Id).ToList();
        var existingDict = await _context.Departments
            .Where(d => d.ClasificacionId.HasValue && ids.Contains(d.ClasificacionId.Value))
            .ToDictionaryAsync(d => d.ClasificacionId!.Value, d => d, cancellationToken);

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.ValorClasificacion))
            {
                result.SkippedCount++;
                progress.Report($"[OMITIDO] Registro ID {dto.Id} omitido porque el nombre del departamento está vacío.");
                continue;
            }

            if (!existingDict.TryGetValue(dto.Id, out var existing))
            {
                var dept = new Department(name: dto.ValorClasificacion, clasificacionId: dto.Id);
                dept.ClearDomainEvents();
                _context.Departments.Add(dept);
                result.CreatedCount++;
                progress.Report($"[NUEVO] Departamento '{dto.ValorClasificacion}' agregado.");
            }
            else
            {
                existing.Update(name: dto.ValorClasificacion, clasificacionId: dto.Id);
                existing.ClearDomainEvents();
                result.UpdatedCount++;
                progress.Report($"[ACTUALIZADO] Departamento '{dto.ValorClasificacion}' actualizado.");
            }
        }
    }

    private async Task SyncCategoriasAsync(List<ClasificacionValorSyncDto> dtos, SyncResult result, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var ids = dtos.Select(d => d.Id).ToList();
        var existingDict = await _context.Categories
            .Where(c => c.ClasificacionId.HasValue && ids.Contains(c.ClasificacionId.Value))
            .ToDictionaryAsync(c => c.ClasificacionId!.Value, c => c, cancellationToken);

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.ValorClasificacion))
            {
                result.SkippedCount++;
                progress.Report($"[OMITIDO] Registro ID {dto.Id} omitido porque el nombre de la categoría está vacío.");
                continue;
            }

            if (!existingDict.TryGetValue(dto.Id, out var existing))
            {
                var cat = new Category(name: dto.ValorClasificacion, clasificacionId: dto.Id);
                cat.ClearDomainEvents();
                _context.Categories.Add(cat);
                result.CreatedCount++;
                progress.Report($"[NUEVO] Categoría '{dto.ValorClasificacion}' agregada.");
            }
            else
            {
                existing.Update(name: dto.ValorClasificacion, clasificacionId: dto.Id);
                existing.ClearDomainEvents();
                result.UpdatedCount++;
                progress.Report($"[ACTUALIZADO] Categoría '{dto.ValorClasificacion}' actualizada.");
            }
        }
    }

    private async Task SyncProductosCatalogAsync(PDV.Domain.Entities.SystemConfiguration config, SyncResult result, IProgress<string> progress, CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        using var httpClient = new HttpClient(handler);
        if (!string.IsNullOrWhiteSpace(config.ComercialApiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", config.ComercialApiKey);
        }

        int page = 1;
        int pageSize = 50;
        bool hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var endpoint = $"{config.ComercialApiUrl!.TrimEnd('/')}/api/Productos?page={page}&pageSize={pageSize}&onlyActive=false";
            progress.Report($"Obteniendo página {page} de productos desde Comercial...");

            var response = await httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"El servidor respondió con código de error {response.StatusCode}. Detalle: {errorBody}");
            }

            var paginatedResult = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ProductoDto>>(cancellationToken: cancellationToken);
            if (paginatedResult == null || paginatedResult.Items == null || !paginatedResult.Items.Any())
            {
                progress.Report($"No se recibieron productos en la página {page}.");
                break;
            }

            progress.Report($"Recibidos {paginatedResult.Items.Count()} productos. Procesando persistencia...");
            await PersistProductosAsync(paginatedResult.Items, result, progress, cancellationToken);

            hasMore = page < paginatedResult.TotalPages;
            page++;
        }
    }

    private async Task PersistProductosAsync(IEnumerable<ProductoDto> dtos, SyncResult result, IProgress<string> progress, CancellationToken cancellationToken)
    {
        // Carga previa en diccionarios para evitar consultas N+1
        var categoryDict = await _context.Categories
            .Where(c => c.ClasificacionId.HasValue && c.ClasificacionId.Value > 0)
            .ToDictionaryAsync(c => c.ClasificacionId!.Value, c => c.Name, cancellationToken);

        var departmentDict = await _context.Departments
            .Where(d => d.ClasificacionId.HasValue && d.ClasificacionId.Value > 0)
            .ToDictionaryAsync(d => d.ClasificacionId!.Value, d => d.Name, cancellationToken);

        var codes = dtos.Select(d => d.Codigo).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var existingProductsDict = await _context.Products
            .Where(p => codes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p, cancellationToken);

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Codigo) || string.IsNullOrWhiteSpace(dto.Nombre))
            {
                result.SkippedCount++;
                progress.Report($"[OMITIDO] Producto omitido por falta de código o nombre.");
                continue;
            }

            // Buscar categoría local por Clasificacion5Id
            string categoryName = dto.Clasificacion5Id > 0 && categoryDict.TryGetValue(dto.Clasificacion5Id, out var catName) ? catName : "";

            // Buscar departamento local por Clasificacion1Id
            string departmentName = dto.Clasificacion1Id > 0 && departmentDict.TryGetValue(dto.Clasificacion1Id, out var deptName) ? deptName : "";

            var mappedControl = dto.ControlExistencia switch
            {
                0 => Domain.Enums.ControlExistencia.SinControl,
                1 => Domain.Enums.ControlExistencia.ConControl,
                2 => Domain.Enums.ControlExistencia.Lotes,
                3 => Domain.Enums.ControlExistencia.Series,
                4 => Domain.Enums.ControlExistencia.Pedimentos,
                _ => Domain.Enums.ControlExistencia.ConControl
            };

            var mappedTax = dto.Impuesto1 == 16.0 ? Domain.Enums.TaxRateType.Rate16 :
                            (dto.Impuesto1 == 8.0 ? Domain.Enums.TaxRateType.Rate8 : Domain.Enums.TaxRateType.ZeroRate);

            var productType = Enum.IsDefined(typeof(Domain.Enums.ProductType), dto.TipoProducto)
                ? (Domain.Enums.ProductType)dto.TipoProducto
                : Domain.Enums.ProductType.Producto;

            if (!existingProductsDict.TryGetValue(dto.Codigo, out var existing))
            {
                var product = new Product(
                    name: dto.Nombre,
                    code: dto.Codigo,
                    price: (decimal)dto.Precio,
                    stock: 0, // Inicia en 0 stock localmente
                    saleType: Domain.Enums.SaleType.Piece,
                    taxRate: mappedTax,
                    category: categoryName,
                    cost: 0,
                    minStock: 0,
                    plu: null,
                    barcode: dto.CodigoAlterno,
                    description: dto.Descripcion,
                    wholesalePrice: (decimal)dto.Precio2,
                    wholesaleMinQuantity: 3,
                    satCode: dto.CodigoSat ?? "",
                    type: productType,
                    controlExistencia: mappedControl,
                    saleUnitId: dto.UnidadMedidaId,
                    saleUnitName: dto.UnidadMedidaNombre,
                    xmlUnitId: dto.IdUnidadXml,
                    department: departmentName,
                    clasificacion1Id: dto.Clasificacion1Id,
                    clasificacion5Id: dto.Clasificacion5Id
                );

                if (!dto.Activo)
                {
                    product.Deactivate();
                }

                product.ClearDomainEvents();
                _context.Products.Add(product);
                result.CreatedCount++;
                progress.Report($"[NUEVO] Producto '{dto.Codigo} - {dto.Nombre}' agregado.");
            }
            else
            {
                existing.UpdateInfo(
                    name: dto.Nombre,
                    description: dto.Descripcion,
                    category: categoryName,
                    satCode: dto.CodigoSat,
                    type: productType,
                    controlExistencia: mappedControl,
                    saleUnitId: dto.UnidadMedidaId,
                    saleUnitName: dto.UnidadMedidaNombre,
                    xmlUnitId: dto.IdUnidadXml,
                    department: departmentName,
                    clasificacion1Id: dto.Clasificacion1Id,
                    clasificacion5Id: dto.Clasificacion5Id
                );

                existing.UpdatePrice((decimal)dto.Precio);
                existing.UpdateWholesalePrice((decimal)dto.Precio2, existing.WholesaleMinQuantity ?? 3);
                existing.UpdateBarcode(dto.CodigoAlterno);
                existing.UpdateTaxRate(mappedTax);

                if (dto.Activo && !existing.IsActive)
                {
                    existing.Activate();
                }
                else if (!dto.Activo && existing.IsActive)
                {
                    existing.Deactivate();
                }

                existing.ClearDomainEvents();
                result.UpdatedCount++;
                progress.Report($"[ACTUALIZADO] Producto '{dto.Codigo} - {dto.Nombre}' actualizado.");
            }
        }
    }

    private class PaginatedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    private class ProductoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public double Precio { get; set; }
        public double Precio2 { get; set; }
        public double Impuesto1 { get; set; }
        public string? CodigoSat { get; set; }
        public int UnidadMedidaId { get; set; }
        public string UnidadMedidaNombre { get; set; } = string.Empty;
        public int IdUnidadXml { get; set; }
        public string? CodigoAlterno { get; set; }
        public int TipoProducto { get; set; }
        public int ControlExistencia { get; set; }
        public int Clasificacion1Id { get; set; }
        public int Clasificacion5Id { get; set; }
        public bool Activo { get; set; }
    }

    private async Task SyncClientesCatalogAsync(PDV.Domain.Entities.SystemConfiguration config, SyncResult result, IProgress<string> progress, CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        using var httpClient = new HttpClient(handler);
        if (!string.IsNullOrWhiteSpace(config.ComercialApiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", config.ComercialApiKey);
        }

        int page = 1;
        int pageSize = 50;
        bool hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var endpoint = $"{config.ComercialApiUrl!.TrimEnd('/')}/api/Clientes?page={page}&pageSize={pageSize}&onlyActive=false";
            progress.Report($"Obteniendo página {page} de clientes desde Comercial...");

            var response = await httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"El servidor respondió con código de error {response.StatusCode}. Detalle: {errorBody}");
            }

            var paginatedResult = await response.Content.ReadFromJsonAsync<PaginatedResultDto<ClienteSyncDto>>(cancellationToken: cancellationToken);
            if (paginatedResult == null || paginatedResult.Items == null || !paginatedResult.Items.Any())
            {
                progress.Report($"No se recibieron clientes en la página {page}.");
                break;
            }

            progress.Report($"Recibidos {paginatedResult.Items.Count()} clientes. Procesando persistencia...");
            await PersistClientesAsync(paginatedResult.Items, result, progress, httpClient, config.ComercialApiUrl, cancellationToken);

            hasMore = page < paginatedResult.TotalPages;
            page++;
        }
    }

    private async Task PersistClientesAsync(IEnumerable<ClienteSyncDto> dtos, SyncResult result, IProgress<string> progress, HttpClient httpClient, string baseUrl, CancellationToken cancellationToken)
    {
        // 1. Carga previa de clientes en memoria
        var codes = dtos.Select(d => d.Codigo).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var existingClientsDict = await _context.Clients
            .Where(c => codes.Contains(c.Code))
            .ToDictionaryAsync(c => c.Code, c => c, cancellationToken);

        // 2. Obtener direcciones asíncronamente en paralelo usando un SemaphoreSlim para limitar la concurrencia
        var clientAddresses = new System.Collections.Concurrent.ConcurrentDictionary<int, Domain.ValueObjects.Address>();
        var semaphore = new SemaphoreSlim(8); // Límite de 8 conexiones concurrentes
        
        var tasks = dtos.Select(async dto =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var addrUrl = $"{baseUrl.TrimEnd('/')}/api/Domicilios?catalogoId={dto.Id}&tipoCatalogo=1";
                var addrResponse = await httpClient.GetAsync(addrUrl, cancellationToken);
                if (addrResponse.IsSuccessStatusCode)
                {
                    var addresses = await addrResponse.Content.ReadFromJsonAsync<List<DomicilioSyncDto>>(cancellationToken: cancellationToken);
                    var fiscalAddr = addresses?.FirstOrDefault(a => a.TipoDireccion == 0);
                    if (fiscalAddr != null && !string.IsNullOrWhiteSpace(fiscalAddr.Calle))
                    {
                        var street = string.IsNullOrWhiteSpace(fiscalAddr.NumeroExterior) 
                            ? fiscalAddr.Calle 
                            : $"{fiscalAddr.Calle} {fiscalAddr.NumeroExterior} {fiscalAddr.NumeroInterior}".Trim();

                        var localAddress = PDV.Domain.ValueObjects.Address.Create(
                            street: street,
                            city: string.IsNullOrWhiteSpace(fiscalAddr.Ciudad) ? "N/A" : fiscalAddr.Ciudad,
                            state: string.IsNullOrWhiteSpace(fiscalAddr.Estado) ? "N/A" : fiscalAddr.Estado,
                            zipCode: string.IsNullOrWhiteSpace(fiscalAddr.CodigoPostal) ? "00000" : fiscalAddr.CodigoPostal,
                            country: string.IsNullOrWhiteSpace(fiscalAddr.Pais) ? "México" : fiscalAddr.Pais
                        );
                        clientAddresses[dto.Id] = localAddress;
                    }
                }
            }
            catch (Exception ex)
            {
                progress.Report($"[ADVERTENCIA] No se pudo obtener dirección para cliente {dto.Codigo}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // 3. Iterar e insertar/actualizar usando los datos en memoria
        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Codigo) || string.IsNullOrWhiteSpace(dto.RazonSocial))
            {
                result.SkippedCount++;
                progress.Report($"[OMITIDO] Cliente omitido por falta de código o razón social.");
                continue;
            }

            clientAddresses.TryGetValue(dto.Id, out var localAddress);

            var taxId = dto.RFC?.Trim() ?? string.Empty;
            if (taxId.Length < 10 || taxId.Length > 13)
            {
                taxId = string.Empty;
            }

            var email = dto.Email?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(email) && (!email.Contains('@') || !email.Contains('.')))
            {
                email = string.Empty;
            }

            if (!existingClientsDict.TryGetValue(dto.Codigo, out var existing))
            {
                var client = new Client(
                    code: dto.Codigo,
                    name: dto.RazonSocial,
                    taxId: taxId,
                    phone: "",
                    email: email
                );

                if (localAddress != null)
                {
                    client.UpdateAddress(localAddress);
                }

                if (!dto.Activo)
                {
                    client.Deactivate();
                }

                client.ClearDomainEvents();
                _context.Clients.Add(client);
                result.CreatedCount++;
                progress.Report($"[NUEVO] Cliente '{dto.Codigo} - {dto.RazonSocial}' agregado.");
            }
            else
            {
                existing.UpdateProfile(dto.RazonSocial, taxId);
                existing.UpdateContactInfo(existing.Phone, email);

                if (localAddress != null)
                {
                    existing.UpdateAddress(localAddress);
                }

                if (dto.Activo && !existing.IsActive)
                {
                    existing.Activate();
                }
                else if (!dto.Activo && existing.IsActive)
                {
                    existing.Deactivate();
                }

                existing.ClearDomainEvents();
                result.UpdatedCount++;
                progress.Report($"[ACTUALIZADO] Cliente '{dto.Codigo} - {dto.RazonSocial}' actualizado.");
            }
        }
    }

    private class ClienteSyncDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string? RFC { get; set; }
        public string? Email { get; set; }
        public bool Activo { get; set; }
    }

    private class DomicilioSyncDto
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

    private class UnidadMedidaSyncDto
    {
        public int Id { get; set; }
        public string NombreUnidad { get; set; } = string.Empty;
        public string Abreviatura { get; set; } = string.Empty;
        public string Despliegue { get; set; } = string.Empty;
        public string ClaveInt { get; set; } = string.Empty;
        public string ClaveSat { get; set; } = string.Empty;
    }

    private class ClasificacionValorSyncDto
    {
        public int Id { get; set; }
        public int ClasificacionId { get; set; }
        public string CodigoValorClasificacion { get; set; } = string.Empty;
        public string ValorClasificacion { get; set; } = string.Empty;
    }
}
