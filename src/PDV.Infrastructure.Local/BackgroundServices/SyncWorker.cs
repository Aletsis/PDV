using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Infrastructure.Persistence;
using PDV.Application.Features.Sync.Dtos;
using PDV.Application.Features.Clients.Queries.GetClientsDelta;
using PDV.Application.Features.Branches.Queries.GetBranchesDelta;
using PDV.Application.Features.Printers.Queries.GetPrintersDelta;
using PDV.Application.Features.TicketSequences.Queries.GetTicketSequencesDelta;
using PDV.Application.Features.FolioSequences.Queries.GetFolioSequencesDelta;
using System.IO;
using PDV.Infrastructure.Local.Discovery;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;

namespace PDV.Infrastructure.Local.BackgroundServices;

public class SyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IClientDiscoveryService _clientDiscoveryService;
    
    private HubConnection? _hubConnection;
    private string? _connectedServerUrl;
    private volatile bool _forcePullSync = false;
    
    private const int MaxAttempts = 5;
    private const int BatchThreshold = 5; // If pending messages > 5, sync in batches
    private const int BatchSize = 30;
    
    // Check interval: short (2 seconds) to keep real-time sync when online
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);

    private DateTime _lastPullExecutionTime = DateTime.MinValue;
    private const string LastPullFilePath = "last-pull-sync.txt";

    public SyncWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SyncWorker> logger,
        IConfiguration configuration,
        IClientDiscoveryService clientDiscoveryService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
        _clientDiscoveryService = clientDiscoveryService;
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        _httpClient = new HttpClient(handler);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncWorker background service started.");

        // Reset failed outbox messages to pending on startup to allow auto-recovery of failed syncs
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var failedCount = await db.OutboxMessages
                .Where(m => m.State == OutboxState.Failed)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, OutboxState.Pending)
                                          .SetProperty(m => m.Attempts, 0), stoppingToken);
            if (failedCount > 0)
            {
                _logger.LogInformation("Reset {Count} failed outbox messages to pending on startup.", failedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting failed outbox messages on startup.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var serverUrl = await _clientDiscoveryService.GetServerUrlAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    _logger.LogWarning("No server URL could be resolved via DNS or auto-discovery. Retrying in 15 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                    continue;
                }

                await EnsureSignalRConnectedAsync(serverUrl, stoppingToken);
                await PerformSynchronizationAsync(serverUrl, stoppingToken);
                await PerformPullSynchronizationAsync(serverUrl, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while running the SyncWorker execution loop.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("SyncWorker background service is stopping.");
    }

    private async Task PerformSynchronizationAsync(string serverBaseUrl, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Check total number of pending messages
        var pendingCount = await db.OutboxMessages
            .CountAsync(m => m.State == OutboxState.Pending, stoppingToken);

        if (pendingCount == 0)
        {
            return;
        }

        // 2. Select execution mode: Batch (post-offline) vs Individual (real-time)
        if (pendingCount > BatchThreshold)
        {
            _logger.LogInformation("Caja was offline or has accumulated lag. Syncing {Count} messages in BATCH mode.", pendingCount);
            await ProcessInBatchModeAsync(db, serverBaseUrl, stoppingToken);
        }
        else
        {
            await ProcessInIndividualModeAsync(db, serverBaseUrl, stoppingToken);
        }
    }

    private async Task ProcessInIndividualModeAsync(AppDbContext db, string serverBaseUrl, CancellationToken stoppingToken)
    {
        var pendingMessages = await db.OutboxMessages
            .Where(m => m.State == OutboxState.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchThreshold)
            .ToListAsync(stoppingToken);

        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/receive";

        foreach (var message in pendingMessages)
        {
            if (stoppingToken.IsCancellationRequested) break;

            message.MarkAsProcessing();
            await db.SaveChangesAsync(stoppingToken);

            var success = false;
            string? errorMessage = null;

            try
            {
                var enrichedPayload = await EnrichPayloadAsync(db, message.EventType, message.Payload, stoppingToken);
                var syncDto = new OutboxSyncDto(message.Id, message.EventType, enrichedPayload, message.CreatedAt);
                var content = new StringContent(JsonSerializer.Serialize(syncDto), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content, stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    success = true;
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync(stoppingToken);
                    errorMessage = $"Server error {response.StatusCode}: {responseBody}";
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
                {
                    _clientDiscoveryService.InvalidateUrl();
                }
            }

            if (success)
            {
                message.MarkAsProcessed();
                _logger.LogInformation("Real-time sync success for outbox message {Id} ({Type}).", message.Id, message.EventType);
            }
            else
            {
                message.MarkAsFailed(errorMessage ?? "Unknown error", MaxAttempts);
                _logger.LogWarning("Real-time sync failed for outbox message {Id}. Attempts: {Attempts}. State: {State}", 
                    message.Id, message.Attempts, message.State);
            }

            await db.SaveChangesAsync(stoppingToken);
        }
    }

    private async Task ProcessInBatchModeAsync(AppDbContext db, string serverBaseUrl, CancellationToken stoppingToken)
    {
        var messages = await db.OutboxMessages
            .Where(m => m.State == OutboxState.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (!messages.Any()) return;

        // Mark all messages in the batch as processing
        foreach (var m in messages)
        {
            m.MarkAsProcessing();
        }
        await db.SaveChangesAsync(stoppingToken);

        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/receive-batch";
        var success = false;
        string? globalError = null;
        BatchSyncResult? batchResult = null;

        try
        {
            var dtos = new List<OutboxSyncDto>();
            foreach (var m in messages)
            {
                var enrichedPayload = await EnrichPayloadAsync(db, m.EventType, m.Payload, stoppingToken);
                dtos.Add(new OutboxSyncDto(m.Id, m.EventType, enrichedPayload, m.CreatedAt));
            }

            var content = new StringContent(JsonSerializer.Serialize(dtos), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content, stoppingToken);

            if (response.IsSuccessStatusCode)
            {
                batchResult = await response.Content.ReadFromJsonAsync<BatchSyncResult>(cancellationToken: stoppingToken);
                success = true;
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(stoppingToken);
                globalError = $"Server returned {response.StatusCode}: {body}";
            }
        }
        catch (Exception ex)
        {
            globalError = ex.Message;
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }

        // Apply results per message
        foreach (var message in messages)
        {
            var isItemSuccess = false;
            string itemError = globalError ?? "Not processed by server";

            if (success && batchResult != null)
            {
                var itemResult = batchResult.Results.FirstOrDefault(r => r.MessageId == message.Id);
                if (itemResult != null)
                {
                    isItemSuccess = itemResult.Success;
                    itemError = itemResult.ErrorMessage ?? "Server processing error";
                }
            }

            if (isItemSuccess)
            {
                message.MarkAsProcessed();
            }
            else
            {
                message.MarkAsFailed(itemError, MaxAttempts);
            }
        }

        await db.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Batch synchronization completed for {Count} messages.", messages.Count);
    }

    private async Task<string> EnrichPayloadAsync(AppDbContext db, string eventType, string originalPayload, CancellationToken cancellationToken)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            if (eventType.StartsWith("Sale"))
            {
                using var doc = JsonDocument.Parse(originalPayload);
                if (doc.RootElement.TryGetProperty("SaleId", out var prop) && prop.TryGetGuid(out var saleId))
                {
                    var sale = await db.Sales
                        .Include(s => s.Items)
                        .Include(s => s.Taxes)
                        .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

                    if (sale != null)
                    {
                        return JsonSerializer.Serialize(sale, jsonOptions);
                    }
                }
            }
            else if (eventType.StartsWith("Shift"))
            {
                using var doc = JsonDocument.Parse(originalPayload);
                if (doc.RootElement.TryGetProperty("ShiftId", out var prop) && prop.TryGetGuid(out var shiftId))
                {
                    var shift = await db.Shifts
                        .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

                    if (shift != null)
                    {
                        return JsonSerializer.Serialize(shift, jsonOptions);
                    }
                }
            }
            else if (eventType.StartsWith("Return"))
            {
                using var doc = JsonDocument.Parse(originalPayload);
                if (doc.RootElement.TryGetProperty("ReturnId", out var prop) && prop.TryGetGuid(out var returnId))
                {
                    var returnEntity = await db.Returns
                        .Include(r => r.Items)
                        .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

                    if (returnEntity != null)
                    {
                        return JsonSerializer.Serialize(returnEntity, jsonOptions);
                    }
                }
            }
            else if (eventType.StartsWith("CashCut"))
            {
                using var doc = JsonDocument.Parse(originalPayload);
                if (doc.RootElement.TryGetProperty("CashCutId", out var prop) && prop.TryGetGuid(out var cashCutId))
                {
                    var cashCut = await db.CashCuts
                        .FirstOrDefaultAsync(c => c.Id == cashCutId, cancellationToken);

                    if (cashCut != null)
                    {
                        return JsonSerializer.Serialize(cashCut, jsonOptions);
                    }
                }
            }
            else if (eventType.StartsWith("Client"))
            {
                using var doc = JsonDocument.Parse(originalPayload);
                if (doc.RootElement.TryGetProperty("ClientId", out var prop) && prop.TryGetGuid(out var clientId))
                {
                    var client = await db.Clients
                        .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

                    if (client != null)
                    {
                        return JsonSerializer.Serialize(client, jsonOptions);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching sync payload for event type {Type}", eventType);
        }

        return originalPayload;
    }

    private DateTime GetLastPullTime()
    {
        try
        {
            if (File.Exists(LastPullFilePath))
            {
                var content = File.ReadAllText(LastPullFilePath);
                if (DateTime.TryParse(content, out var dt))
                {
                    return dt.ToUniversalTime();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read last pull sync file. Defaulting to DateTime.MinValue.");
        }
        return DateTime.MinValue;
    }

    private void SaveLastPullTime(DateTime dt)
    {
        try
        {
            File.WriteAllText(LastPullFilePath, dt.ToString("o"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write last pull sync file.");
        }
    }

    private async Task PerformPullSynchronizationAsync(string serverBaseUrl, CancellationToken stoppingToken)
    {
        if (!_forcePullSync && DateTime.UtcNow - _lastPullExecutionTime < TimeSpan.FromSeconds(30))
        {
            return;
        }
        _forcePullSync = false;
        _lastPullExecutionTime = DateTime.UtcNow;

        var lastPullTime = GetLastPullTime();
        var currentSyncStartTime = DateTime.UtcNow;

        _logger.LogInformation("Starting pull synchronization from server (Last sync: {LastPullTime}).", lastPullTime);

        // 1. Pull Clients
        await PullClientsDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 2. Pull Products
        await PullProductsDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 3. Pull Branches
        await PullBranchesDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 3.5. Pull Folio Sequences
        await PullFolioSequencesDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 4. Pull UnidadesMedida
        await PullUnidadesMedidaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 5. Pull Printers
        await PullPrintersDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 6. Pull Cash Registers
        await PullCashRegistersDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 6.5. Pull Ticket Sequences
        await PullTicketSequencesDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        // 7. Pull Users
        await PullUsersDeltaAsync(serverBaseUrl, lastPullTime, stoppingToken);

        SaveLastPullTime(currentSyncStartTime);
    }

    private async Task PullClientsDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/clients-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Clients pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<ClientSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} client deltas from server. Applying locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in deltas)
            {
                var taxId = dto.TaxId?.Trim() ?? string.Empty;
                if (taxId.Length < 10 || taxId.Length > 13)
                {
                    taxId = string.Empty;
                }

                var email = dto.Email?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(email) && (!email.Contains('@') || !email.Contains('.')))
                {
                    email = string.Empty;
                }

                var phone = dto.Phone?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(phone) && (phone.Length < 10 || !phone.All(char.IsDigit)))
                {
                    phone = string.Empty;
                }

                var existing = await db.Clients.FirstOrDefaultAsync(c => c.Id == dto.Id, stoppingToken);
                if (existing == null)
                {
                    var client = new Client(dto.Code, dto.Name, taxId, phone, email);
                    client.SetId(dto.Id);

                    if (!string.IsNullOrWhiteSpace(dto.Street))
                    {
                        var address = Domain.ValueObjects.Address.Create(
                            dto.Street, 
                            dto.City ?? "N/A", 
                            dto.State ?? "N/A", 
                            dto.ZipCode ?? "00000", 
                            dto.Country ?? "México");
                        client.UpdateAddress(address);
                    }

                    if (!dto.IsActive)
                    {
                        client.Deactivate();
                    }

                    client.ClearDomainEvents();
                    db.Clients.Add(client);
                }
                else
                {
                    existing.ChangeCode(dto.Code);
                    existing.UpdateProfile(dto.Name, taxId);
                    existing.UpdateContactInfo(phone, email);

                    if (!string.IsNullOrWhiteSpace(dto.Street))
                    {
                        var address = Domain.ValueObjects.Address.Create(
                            dto.Street, 
                            dto.City ?? "N/A", 
                            dto.State ?? "N/A", 
                            dto.ZipCode ?? "00000", 
                            dto.Country ?? "México");
                        existing.UpdateAddress(address);
                    }

                    if (dto.IsActive && !existing.IsActive)
                    {
                        existing.Activate();
                    }
                    else if (!dto.IsActive && existing.IsActive)
                    {
                        existing.Deactivate();
                    }

                    existing.ClearDomainEvents();
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} client deltas to local SQLite.", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during client pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullProductsDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/products-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Products pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<PDV.Application.Features.Products.Queries.GetProductsDelta.ProductSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} product deltas from server. Applying locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in deltas)
            {
                var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == dto.Id, stoppingToken);
                var saleType = Enum.TryParse<PDV.Domain.Enums.SaleType>(dto.SaleType, true, out var st) ? st : PDV.Domain.Enums.SaleType.Piece;
                var taxRate = Enum.TryParse<PDV.Domain.Enums.TaxRateType>(dto.TaxRate, true, out var tr) ? tr : PDV.Domain.Enums.TaxRateType.Rate16;

                if (existing == null)
                {
                    var product = new Product(
                        name: dto.Name,
                        code: dto.Code,
                        price: dto.Price,
                        stock: dto.Stock,
                        saleType: saleType,
                        taxRate: taxRate,
                        category: dto.Category,
                        cost: dto.Cost,
                        minStock: dto.MinStock,
                        plu: dto.Plu,
                        barcode: dto.Barcode,
                        description: dto.Description,
                        satCode: dto.SatCode,
                        type: (PDV.Domain.Enums.ProductType)dto.Type,
                        controlExistencia: (PDV.Domain.Enums.ControlExistencia)dto.ControlExistencia,
                        saleUnitId: dto.SaleUnitId,
                        saleUnitName: dto.SaleUnitName,
                        xmlUnitId: dto.XmlUnitId,
                        department: dto.Department,
                        clasificacion1Id: dto.Clasificacion1Id,
                        clasificacion5Id: dto.Clasificacion5Id
                    );
                    product.SetId(dto.Id);

                    if (dto.WholesalePrice.HasValue)
                    {
                        product.UpdateWholesalePrice(dto.WholesalePrice, dto.WholesaleMinQuantity);
                    }

                    if (!dto.IsActive)
                    {
                        product.Deactivate();
                    }

                    product.ClearDomainEvents();
                    db.Products.Add(product);
                }
                else
                {
                    existing.UpdateInfo(
                        name: dto.Name, 
                        description: dto.Description, 
                        category: dto.Category,
                        satCode: dto.SatCode,
                        type: (PDV.Domain.Enums.ProductType)dto.Type,
                        controlExistencia: (PDV.Domain.Enums.ControlExistencia)dto.ControlExistencia,
                        saleUnitId: dto.SaleUnitId,
                        saleUnitName: dto.SaleUnitName,
                        xmlUnitId: dto.XmlUnitId,
                        department: dto.Department,
                        clasificacion1Id: dto.Clasificacion1Id,
                        clasificacion5Id: dto.Clasificacion5Id
                    );
                    if (existing.Code != dto.Code)
                    {
                        existing.ChangeCode(dto.Code);
                    }
                    existing.UpdatePrice(dto.Price);
                    existing.UpdateWholesalePrice(dto.WholesalePrice, dto.WholesaleMinQuantity);
                    existing.AdjustStock(dto.Stock);
                    existing.UpdatePlu(dto.Plu);
                    existing.UpdateBarcode(dto.Barcode);
                    existing.UpdateCost(dto.Cost);
                    existing.UpdateMinStock(dto.MinStock);
                    existing.ChangeSaleType(saleType);
                    existing.UpdateTaxRate(taxRate);

                    if (dto.IsActive && !existing.IsActive)
                    {
                        existing.Activate();
                    }
                    else if (!dto.IsActive && existing.IsActive)
                    {
                        existing.Deactivate();
                    }

                    existing.ClearDomainEvents();
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} product deltas to local SQLite.", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during product pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullBranchesDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/branches-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Branches pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<BranchSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} branch deltas from server. Applying locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in deltas)
            {
                var existing = await db.Branches.FirstOrDefaultAsync(b => b.Id == dto.Id, stoppingToken);

                Domain.ValueObjects.Address? address = null;
                if (!string.IsNullOrWhiteSpace(dto.Street))
                {
                    address = Domain.ValueObjects.Address.Create(
                        dto.Street, 
                        dto.City ?? "N/A", 
                        dto.State ?? "N/A", 
                        dto.ZipCode ?? "00000", 
                        dto.Country ?? "México");
                }

                if (existing == null)
                {
                    var branch = new Branch(
                        name: dto.Name,
                        code: dto.Code,
                        address: address,
                        phone: dto.Phone,
                        email: dto.Email,
                        isMainBranch: dto.IsMainBranch
                    );
                    branch.SetId(dto.Id);

                    if (!dto.IsActive)
                    {
                        branch.Deactivate();
                    }

                    branch.ClearDomainEvents();
                    db.Branches.Add(branch);
                }
                else
                {
                    existing.Update(dto.Name, address, dto.Phone, dto.Email);

                    if (dto.IsMainBranch && !existing.IsMainBranch)
                    {
                        existing.SetAsMainBranch();
                    }

                    if (dto.IsActive && !existing.IsActive)
                    {
                        existing.Activate();
                    }
                    else if (!dto.IsActive && existing.IsActive)
                    {
                        existing.Deactivate();
                    }

                    existing.ClearDomainEvents();
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} branch deltas to local SQLite.", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during branch pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullUnidadesMedidaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/unidades-medida?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UnidadesMedida pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var units = await response.Content.ReadFromJsonAsync<List<UnidadMedidaSyncDto>>(cancellationToken: stoppingToken);
            if (units == null || !units.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} units of measure from server. Syncing locally...", units.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in units)
            {
                var existing = await db.UnidadesMedida.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.ExternalId == dto.ExternalId, stoppingToken);
                if (existing == null)
                {
                    var unit = new UnidadMedida(
                        externalId: dto.ExternalId,
                        nombreUnidad: dto.NombreUnidad,
                        abreviatura: dto.Abreviatura,
                        despliegue: dto.Despliegue,
                        claveInt: dto.ClaveInt,
                        claveSat: dto.ClaveSat
                    );
                    unit.ClearDomainEvents();
                    db.UnidadesMedida.Add(unit);
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
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} units of measure to local SQLite.", units.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during UnidadesMedida pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullCashRegistersDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/cash-registers-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cash registers pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<CashRegisterSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} cash register deltas from server. Syncing locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in deltas)
            {
                var existing = await db.CashRegisters.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == dto.Id, stoppingToken);
                var mode = (PDV.Domain.Enums.CashRegisterMode)dto.Mode;

                if (existing == null)
                {
                    var cashRegister = new CashRegister(
                        name: dto.Name,
                        location: dto.Location,
                        branchId: dto.BranchId,
                        mode: mode
                    );
                    cashRegister.SetId(dto.Id);
                    cashRegister.BindToIp(dto.IpAddress);
                    cashRegister.AssignUser(dto.AssignedUserId);
                    cashRegister.AssignPrinter(dto.AssignedPrinterId);

                    if (!dto.IsActive)
                    {
                        cashRegister.Deactivate();
                    }

                    cashRegister.ClearDomainEvents();
                    db.CashRegisters.Add(cashRegister);
                }
                else
                {
                    existing.Update(dto.Name, dto.Location);
                    existing.BindToIp(dto.IpAddress);
                    existing.AssignUser(dto.AssignedUserId);
                    existing.AssignPrinter(dto.AssignedPrinterId);

                    if (dto.IsActive && !existing.IsActive)
                    {
                        existing.Activate();
                    }
                    else if (!dto.IsActive && existing.IsActive)
                    {
                        existing.Deactivate();
                    }

                    existing.ClearDomainEvents();
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} cash register deltas to local SQLite.", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during cash registers pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullPrintersDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/printers-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Printers pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<PrinterSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} printer deltas from server. Syncing locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in deltas)
            {
                var existing = await db.Printers.FirstOrDefaultAsync(p => p.Id == dto.Id, stoppingToken);

                if (existing == null)
                {
                    var printer = new Printer(
                        name: dto.Name,
                        connectionType: dto.ConnectionType,
                        codePage: dto.CodePage,
                        maxWidth: dto.MaxWidth,
                        ipAddress: dto.IpAddress,
                        port: dto.Port,
                        devicePath: dto.DevicePath,
                        branchId: dto.BranchId
                    );
                    printer.SetId(dto.Id);

                    if (!dto.IsActive)
                    {
                        printer.Deactivate();
                    }

                    printer.ClearDomainEvents();
                    db.Printers.Add(printer);
                }
                else
                {
                    existing.Update(
                        name: dto.Name,
                        codePage: dto.CodePage,
                        maxWidth: dto.MaxWidth,
                        ipAddress: dto.IpAddress,
                        port: dto.Port,
                        devicePath: dto.DevicePath
                    );

                    db.Entry(existing).Property(x => x.ConnectionType).CurrentValue = dto.ConnectionType;
                    db.Entry(existing).Property(x => x.BranchId).CurrentValue = dto.BranchId;

                    if (dto.IsActive && !existing.IsActive)
                    {
                        existing.Activate();
                    }
                    else if (!dto.IsActive && existing.IsActive)
                    {
                        existing.Deactivate();
                    }

                    existing.ClearDomainEvents();
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} printer deltas to local SQLite.", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during printers pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullUsersDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/users-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<UserSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} user deltas from server. Syncing locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PDV.Infrastructure.Identity.ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();

            foreach (var dto in deltas)
            {
                var user = await userManager.FindByIdAsync(dto.Id);
                if (user == null)
                {
                    user = new PDV.Infrastructure.Identity.ApplicationUser
                    {
                        Id = dto.Id,
                        UserName = dto.UserName,
                        Email = dto.Email,
                        FullName = dto.FullName,
                        IsActive = dto.IsActive,
                        PasswordHash = dto.PasswordHash
                    };

                    var result = await userManager.CreateAsync(user);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Successfully created offline user profile: {Username}", dto.UserName);
                        
                        foreach (var role in dto.Roles)
                        {
                            if (!await roleManager.RoleExistsAsync(role))
                            {
                                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(role));
                            }
                            await userManager.AddToRoleAsync(user, role);
                        }
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to create user {Username} locally: {Errors}", dto.UserName, errors);
                    }
                }
                else
                {
                    user.UserName = dto.UserName;
                    user.Email = dto.Email;
                    user.FullName = dto.FullName;
                    user.IsActive = dto.IsActive;
                    user.PasswordHash = dto.PasswordHash;

                    var result = await userManager.UpdateAsync(user);
                    if (result.Succeeded)
                    {
                        var currentRoles = await userManager.GetRolesAsync(user);
                        var rolesToRemove = currentRoles.Except(dto.Roles).ToList();
                        var rolesToAdd = dto.Roles.Except(currentRoles).ToList();

                        if (rolesToRemove.Any())
                        {
                            await userManager.RemoveFromRolesAsync(user, rolesToRemove);
                        }

                        foreach (var role in rolesToAdd)
                        {
                            if (!await roleManager.RoleExistsAsync(role))
                            {
                                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(role));
                            }
                            await userManager.AddToRoleAsync(user, role);
                        }
                        
                        _logger.LogInformation("Successfully updated offline user profile: {Username}", dto.UserName);
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to update user {Username} locally: {Errors}", dto.UserName, errors);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during users pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullTicketSequencesDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/ticket-sequences-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ticket sequences pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<TicketSequenceSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} ticket sequence deltas from server. Syncing locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in deltas)
            {
                var existing = await db.TicketSequences.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == dto.Id, stoppingToken);

                if (existing == null)
                {
                    var entity = new TicketSequence(dto.CashRegisterId, dto.SequenceType, dto.Series);
                    entity.SetId(dto.Id);
                    entity.ResetTo(dto.LastTicketNumber);
                    
                    db.Entry(entity).Property(x => x.ResetOnNewShift).CurrentValue = dto.ResetOnNewShift;

                    if (dto.IsDeleted)
                    {
                        entity.SoftDelete("SystemSync");
                    }
                    entity.ClearDomainEvents();
                    db.TicketSequences.Add(entity);
                }
                else
                {
                    existing.UpdateSeries(dto.Series);
                    existing.ResetTo(dto.LastTicketNumber);
                    db.Entry(existing).Property(x => x.ResetOnNewShift).CurrentValue = dto.ResetOnNewShift;
                    db.Entry(existing).Property(x => x.CashRegisterId).CurrentValue = dto.CashRegisterId;
                    db.Entry(existing).Property(x => x.SequenceType).CurrentValue = dto.SequenceType;

                    if (dto.IsDeleted && !existing.IsDeleted)
                    {
                        existing.SoftDelete("SystemSync");
                    }
                    else if (!dto.IsDeleted && existing.IsDeleted)
                    {
                        existing.Restore();
                    }

                    existing.ClearDomainEvents();
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} ticket sequence deltas to local SQLite.", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during ticket sequences pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private async Task PullFolioSequencesDeltaAsync(string serverBaseUrl, DateTime lastPullTime, CancellationToken stoppingToken)
    {
        var endpoint = $"{serverBaseUrl.TrimEnd('/')}/api/sync/folio-sequences-delta?since={Uri.EscapeDataString(lastPullTime.ToString("o"))}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Folio sequences pull sync failed. Server returned: {StatusCode}", response.StatusCode);
                return;
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<FolioSequenceSyncDto>>(cancellationToken: stoppingToken);
            if (deltas == null || !deltas.Any())
            {
                return;
            }

            _logger.LogInformation("Received {Count} folio sequence deltas from server. Syncing locally...", deltas.Count);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var dto in deltas)
            {
                var existing = await db.FolioSequences.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == dto.Id, stoppingToken);

                if (existing == null)
                {
                    var entity = new FolioSequence(dto.BranchId, dto.SeriesType, dto.Series, dto.FolioDigits);
                    entity.SetId(dto.Id);
                    entity.ResetTo(dto.LastFolio);
                    if (dto.ConceptCode != null)
                    {
                        entity.UpdateConcept(dto.ConceptCode, dto.Series, dto.LastFolio);
                    }

                    if (dto.IsDeleted)
                    {
                        entity.SoftDelete("SystemSync");
                    }
                    entity.ClearDomainEvents();
                    db.FolioSequences.Add(entity);
                }
                else
                {
                    existing.UpdateConcept(dto.ConceptCode, dto.Series, dto.LastFolio);
                    db.Entry(existing).Property(x => x.BranchId).CurrentValue = dto.BranchId;
                    db.Entry(existing).Property(x => x.SeriesType).CurrentValue = dto.SeriesType;
                    db.Entry(existing).Property(x => x.FolioDigits).CurrentValue = dto.FolioDigits;

                    if (dto.IsDeleted && !existing.IsDeleted)
                    {
                        existing.SoftDelete("SystemSync");
                    }
                    else if (!dto.IsDeleted && existing.IsDeleted)
                    {
                        existing.Restore();
                    }

                    existing.ClearDomainEvents();
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Successfully applied {Count} folio sequence deltas to local SQLite.", deltas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during folio sequences pull synchronization.");
            if (ex is HttpRequestException or SocketException or TimeoutException or System.Net.WebException)
            {
                _clientDiscoveryService.InvalidateUrl();
            }
        }
    }

    private class UnidadMedidaSyncDto
    {
        public Guid Id { get; set; }
        public int ExternalId { get; set; }
        public string NombreUnidad { get; set; } = string.Empty;
        public string Abreviatura { get; set; } = string.Empty;
        public string Despliegue { get; set; } = string.Empty;
        public string ClaveInt { get; set; } = string.Empty;
        public string ClaveSat { get; set; } = string.Empty;
    }

    private class CashRegisterSyncDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? IpAddress { get; set; }
        public int Mode { get; set; }
        public Guid BranchId { get; set; }
        public string? AssignedUserId { get; set; }
        public Guid? AssignedPrinterId { get; set; }
    }

    private class UserSyncDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? PasswordHash { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    private async Task EnsureSignalRConnectedAsync(string serverBaseUrl, CancellationToken stoppingToken)
    {
        if (_hubConnection != null && _connectedServerUrl == serverBaseUrl)
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                try
                {
                    _logger.LogInformation("SignalR connection is disconnected. Retrying connection...");
                    await _hubConnection.StartAsync(stoppingToken);
                    _logger.LogInformation("SignalR connection re-established.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reconnect to SignalR hub. Will retry in next cycle.");
                }
            }
            return;
        }

        if (_hubConnection != null)
        {
            _logger.LogInformation("Server URL changed or first initialization. Disposing old SignalR connection...");
            try
            {
                await _hubConnection.StopAsync(CancellationToken.None);
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing old SignalR connection.");
            }
            _hubConnection = null;
            _connectedServerUrl = null;
        }

        var hubUrl = $"{serverBaseUrl.TrimEnd('/')}/hubs/sync";
        _logger.LogInformation("Initializing new SignalR connection to: {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = 
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        _hubConnection.On<string>("ReceiveSyncNotification", (entityName) =>
        {
            _logger.LogInformation("Real-time sync notification received for: {EntityName}. Triggering immediate pull.", entityName);
            _forcePullSync = true;
        });

        _hubConnection.Closed += async (error) =>
        {
            _logger.LogWarning(error, "SignalR connection closed.");
            await Task.CompletedTask;
        };

        _hubConnection.Reconnecting += async (error) =>
        {
            _logger.LogInformation(error, "SignalR reconnecting...");
            await Task.CompletedTask;
        };

        _hubConnection.Reconnected += async (connectionId) =>
        {
            _logger.LogInformation("SignalR reconnected. ConnectionId: {ConnectionId}", connectionId);
            _forcePullSync = true;
            await Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync(stoppingToken);
            _connectedServerUrl = serverBaseUrl;
            _logger.LogInformation("SignalR connection established successfully. ConnectionId: {ConnectionId}", _hubConnection.ConnectionId);
            _forcePullSync = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub at {HubUrl}", hubUrl);
        }
    }

    public override void Dispose()
    {
        if (_hubConnection != null)
        {
            try
            {
                _hubConnection.DisposeAsync().GetAwaiter().GetResult();
            }
            catch { }
        }
        base.Dispose();
    }
}
