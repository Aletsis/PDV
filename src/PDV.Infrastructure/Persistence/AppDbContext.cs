using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Infrastructure.Identity;

namespace PDV.Infrastructure.Persistence;

/// <summary>
/// Unidad de persistencia principal. Responsabilidades:
///   - Exponer DbSets para todas las entidades del dominio.
///   - Aplicar configuraciones de mapeo (Configurations/*.cs).
///   - Gestionar el ciclo de vida de las transacciones explícitas.
///
/// El procesamiento de eventos de dominio y la escritura del Outbox
/// son responsabilidad de <see cref="Interceptors.DomainEventsInterceptor"/>.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private readonly IRealTimeSyncNotifier? _syncNotifier;
    private readonly IDateTimeService? _dateTimeService;
    private readonly IAuditService? _auditService;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;
    private IDbContextTransaction? _currentTransaction;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService? currentUserService = null,
        IRealTimeSyncNotifier? syncNotifier = null,
        IDateTimeService? dateTimeService = null,
        IAuditService? auditService = null,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _currentUserService = currentUserService;
        _syncNotifier = syncNotifier;
        _dateTimeService = dateTimeService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    // ──────────────────────────────────────────────
    // DbSets
    // ──────────────────────────────────────────────
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductBranchStock> ProductBranchStocks { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }

    public DbSet<CashRegister> CashRegisters { get; set; }
    public DbSet<CashCut> CashCuts { get; set; }
    public DbSet<CashCutReconciliation> CashCutReconciliations { get; set; }
    public DbSet<CashCollection> CashCollections { get; set; }
    public DbSet<Cancellation> Cancellations { get; set; }
    public DbSet<Return> Returns { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Logo> Logos { get; set; }
    public DbSet<Printer> Printers { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<SystemConfiguration> SystemConfigurations { get; set; }
    public DbSet<FolioSequence> FolioSequences { get; set; }
    public DbSet<TicketSequence> TicketSequences { get; set; }
    public DbSet<TicketTemplate> TicketTemplates { get; set; }
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InventoryMovement> InventoryMovements { get; set; }
    public DbSet<UnidadMedida> UnidadesMedida { get; set; }
    public DbSet<ContpaqiSyncQueue> ContpaqiSyncQueues { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<PriceList> PriceLists { get; set; }
    public DbSet<PriceListProduct> PriceListProducts { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }
    public DbSet<SyncConflict> SyncConflicts { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<DeliveryZone> DeliveryZones { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<DeliveryRoute> DeliveryRoutes { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<InventoryDocument> InventoryDocuments { get; set; }
    public DbSet<InventoryDocumentItem> InventoryDocumentItems { get; set; }
    public DbSet<InventoryConceptMapping> InventoryConceptMappings { get; set; }
    public DbSet<UserWorkStatus> UserWorkStatuses { get; set; }

    // ──────────────────────────────────────────────
    // Configuración del modelo
    // ──────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Registrar extensión pg_trgm para búsquedas de texto predictivo con GIN en PostgreSQL
        builder.HasAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

        // Configurar convertidores de valor en SQLite para evitar problemas con BLOBs y sensibilidad a mayúsculas en GUIDs
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var sqliteRowVersionConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<byte[], string>(
                v => Convert.ToBase64String(v),
                v => Convert.FromBase64String(v));

            var sqliteGuidConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Guid, string>(
                v => v.ToString().ToLowerInvariant(),
                v => Guid.Parse(v));

            var sqliteNullableGuidConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Guid?, string>(
                v => v.HasValue ? v.Value.ToString().ToLowerInvariant() : null!,
                v => string.IsNullOrEmpty(v) ? null : Guid.Parse(v));

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var rowVersionProp = entityType.FindProperty("RowVersion");
                if (rowVersionProp != null && rowVersionProp.ClrType == typeof(byte[]))
                {
                    rowVersionProp.SetValueConverter(sqliteRowVersionConverter);
                }

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(Guid))
                    {
                        property.SetValueConverter(sqliteGuidConverter);
                    }
                    else if (property.ClrType == typeof(Guid?))
                    {
                        property.SetValueConverter(sqliteNullableGuidConverter);
                    }
                }
            }
        }

        // Aplica automáticamente todos los IEntityTypeConfiguration<T>
        // definidos en el ensamblado de Infraestructura (Configurations/*.cs)
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Configurar filtros globales para Soft Delete en todas las entidades de BaseEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(PDV.Domain.Common.BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, "IsDeleted");
                var falseConstant = System.Linq.Expressions.Expression.Constant(false);
                var body = System.Linq.Expressions.Expression.Equal(property, falseConstant);
                var lambda = System.Linq.Expressions.Expression.Lambda(body, parameter);

                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    // ──────────────────────────────────────────────
    // Gestión de transacciones explícitas
    // ──────────────────────────────────────────────
    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_currentTransaction != null) return;
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (_currentTransaction == null) return;
        await _currentTransaction.CommitAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (_currentTransaction == null) return;
        await _currentTransaction.RollbackAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public override int SaveChanges()
    {
        // 1. Validar inmutabilidad de AuditLog
        var hasAuditLogChanges = ChangeTracker.Entries<AuditLog>()
            .Any(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);
        if (hasAuditLogChanges)
        {
            throw new InvalidOperationException("Los registros de auditoría son inmutables y no se pueden modificar ni eliminar.");
        }

        ApplyAuditInfo();
        
        // 2. Extraer entradas de auditoría ANTES de persistir
        var auditEntries = OnBeforeSaveChanges();

        var modifiedEntities = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .Select(e => e.Entity.GetType().Name)
            .Distinct()
            .ToList();

        var result = base.SaveChanges();

        // 3. Escribir y guardar auditoría DESPUÉS de guardar cambios
        OnAfterSaveChangesSync(auditEntries);

        if (result > 0 && _syncNotifier != null && modifiedEntities.Any())
        {
            var syncEntities = new HashSet<string> { "Product", "Client", "CashRegister", "ApplicationUser", "Printer", "FolioSequence", "TicketSequence", "Branch", "ProductBranchStock" };
            foreach (var entityName in modifiedEntities)
            {
                var cleanName = entityName.Split('_').First();
                if (syncEntities.Contains(cleanName))
                {
                    try
                    {
                        _syncNotifier.NotifyEntityChangedAsync(cleanName, CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Ignorar errores del notificador para no comprometer la transacción persistida
                    }
                }
            }
        }

        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Validar inmutabilidad de AuditLog
        var hasAuditLogChanges = ChangeTracker.Entries<AuditLog>()
            .Any(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);
        if (hasAuditLogChanges)
        {
            throw new InvalidOperationException("Los registros de auditoría son inmutables y no se pueden modificar ni eliminar.");
        }

        ApplyAuditInfo();
        
        // 2. Extraer entradas de auditoría ANTES de persistir
        var auditEntries = OnBeforeSaveChanges();

        var modifiedEntities = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .Select(e => e.Entity.GetType().Name)
            .Distinct()
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // 3. Escribir y guardar auditoría DESPUÉS de guardar cambios
        await OnAfterSaveChanges(auditEntries, cancellationToken);

        if (result > 0 && _syncNotifier != null && modifiedEntities.Any())
        {
            var syncEntities = new HashSet<string> { "Product", "Client", "CashRegister", "ApplicationUser", "Printer", "FolioSequence", "TicketSequence", "Branch", "ProductBranchStock" };
            foreach (var entityName in modifiedEntities)
            {
                var cleanName = entityName.Split('_').First();
                if (syncEntities.Contains(cleanName))
                {
                    try
                    {
                        await _syncNotifier.NotifyEntityChangedAsync(cleanName, cancellationToken);
                    }
                    catch
                    {
                        // Ignorar errores del notificador para no comprometer la transacción persistida
                    }
                }
            }
        }

        return result;
    }

    private List<AuditEntry> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();
        var userId = _currentUserService?.UserId ?? "System";
        var ipAddress = _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            // Evitar auditar tablas técnicas o de sincronización interna
            var entityName = entry.Entity.GetType().Name;
            if (entityName == "OutboxMessage" || entityName == "InboxMessage" || entityName == "SyncConflict")
                continue;

            var auditEntry = new AuditEntry(entry)
            {
                UserId = userId,
                IpAddress = ipAddress,
                TableName = entityName,
                Action = entry.State.ToString()
            };

            auditEntries.Add(auditEntry);

            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        return auditEntries;
    }

    private async Task OnAfterSaveChanges(List<AuditEntry> auditEntries, CancellationToken cancellationToken)
    {
        if (auditEntries == null || auditEntries.Count == 0) return;

        var timestamp = _dateTimeService?.UtcNow ?? DateTime.UtcNow;

        foreach (var auditEntry in auditEntries)
        {
            foreach (var prop in auditEntry.Entry.Properties)
            {
                if (prop.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                }
            }

            var log = auditEntry.ToAuditLog(timestamp, _auditService?.CurrentAction);
            AuditLogs.Add(log);
        }

        await base.SaveChangesAsync(cancellationToken);
    }

    private void OnAfterSaveChangesSync(List<AuditEntry> auditEntries)
    {
        if (auditEntries == null || auditEntries.Count == 0) return;

        var timestamp = _dateTimeService?.UtcNow ?? DateTime.UtcNow;

        foreach (var auditEntry in auditEntries)
        {
            foreach (var prop in auditEntry.Entry.Properties)
            {
                if (prop.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                }
            }

            var log = auditEntry.ToAuditLog(timestamp, _auditService?.CurrentAction);
            AuditLogs.Add(log);
        }

        base.SaveChanges();
    }

    private void ApplyAuditInfo()
    {
        var userName = _currentUserService?.UserName ?? "System";

        foreach (var entry in ChangeTracker.Entries<PDV.Domain.Common.BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreationAudit(userName);
                    if (entry.Entity is ProductBranchStock pbsAdd)
                    {
                        pbsAdd.RowVersion = Guid.NewGuid().ToByteArray();
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.SetModificationAudit(userName);
                    if (entry.Entity is ProductBranchStock pbsMod)
                    {
                        pbsMod.RowVersion = Guid.NewGuid().ToByteArray();
                    }
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.SoftDelete(userName);
                    if (entry.Entity is ProductBranchStock pbsDel)
                    {
                        pbsDel.RowVersion = Guid.NewGuid().ToByteArray();
                    }
                    break;
            }
        }
    }
}
