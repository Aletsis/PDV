using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;

namespace PDV.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Category> Categories { get; }
    DbSet<Department> Departments { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }

    DbSet<CashRegister> CashRegisters { get; }
    DbSet<CashCut> CashCuts { get; }
    DbSet<CashCollection> CashCollections { get; }
    DbSet<Cancellation> Cancellations { get; }
    DbSet<Return> Returns { get; }
    DbSet<Client> Clients { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Logo> Logos { get; }
    DbSet<Printer> Printers { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<UnidadMedida> UnidadesMedida { get; }
    DbSet<FolioSequence> FolioSequences { get; }
    DbSet<TicketSequence> TicketSequences { get; }
    DbSet<SystemConfiguration> SystemConfigurations { get; }
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
}

public class UserSyncDataDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? PasswordHash { get; set; }
    public List<string> Roles { get; set; } = new();
}

public interface IIdentityService
{
    Task<List<UserSyncDataDto>> GetUsersDeltaAsync(DateTime sinceUtc, CancellationToken cancellationToken);
    Task<List<UserSyncDataDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<UserSyncDataDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken);
}
