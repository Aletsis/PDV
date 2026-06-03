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
    private IDbContextTransaction? _currentTransaction;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService? currentUserService = null) : base(options)
    {
        _currentUserService = currentUserService;
    }

    // ──────────────────────────────────────────────
    // DbSets
    // ──────────────────────────────────────────────
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }

    public DbSet<CashRegister> CashRegisters { get; set; }
    public DbSet<CashCut> CashCuts { get; set; }
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
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InventoryMovement> InventoryMovements { get; set; }
    public DbSet<UnidadMedida> UnidadesMedida { get; set; }

    // ──────────────────────────────────────────────
    // Configuración del modelo
    // ──────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
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
                    if (entry.Entity is Product pAdd)
                    {
                        pAdd.RowVersion = Guid.NewGuid().ToByteArray();
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.SetModificationAudit(userName);
                    if (entry.Entity is Product pMod)
                    {
                        pMod.RowVersion = Guid.NewGuid().ToByteArray();
                    }
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.SoftDelete(userName);
                    if (entry.Entity is Product pDel)
                    {
                        pDel.RowVersion = Guid.NewGuid().ToByteArray();
                    }
                    break;
            }
        }
    }
}
