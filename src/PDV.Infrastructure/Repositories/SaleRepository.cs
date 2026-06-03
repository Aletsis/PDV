using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

/// <summary>
/// Implementación del repositorio de Sale usando EF Core
/// </summary>
public class SaleRepository : ISaleRepository
{
    private readonly AppDbContext _context;

    public SaleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Sales.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Sale?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Sale>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sales.ToListAsync(cancellationToken);
    }

    public Task<int> AddAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        _context.Sales.Add(sale);
        return Task.FromResult(0);
    }

    public Task UpdateAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        _context.Sales.Update(sale);
        return Task.CompletedTask;
    }

    public async Task<List<Sale>> GetByPaymentMethodAsync(PDV.Domain.Enums.PaymentMethodType paymentMethod, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.PaymentMethod == paymentMethod)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByShiftIdAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.ShiftId == shiftId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Sale?> GetByFolioAsync(Guid? cashRegisterId, string series, int folio, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .FirstOrDefaultAsync(s => s.CashRegisterId == cashRegisterId && s.Series == series && s.Folio == folio, cancellationToken);
    }

    public async Task<List<Sale>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.ClientId == clientId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByCashRegisterIdAsync(Guid cashRegisterId, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.CashRegisterId == cashRegisterId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.Sales
            .Where(s => s.Date.Date == targetDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.Date >= startDate && s.Date <= endDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByIsPaidAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.IsPaid)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByIsNotPaidAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => !s.IsPaid)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByIsCancelledAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.IsCancelled)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByInvoicedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.IsInvoiced)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByNotInvoicedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => !s.IsInvoiced)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByInvoiceIdAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Where(s => s.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sale>> GetByCriteriaAsync(
        Guid? clientId, 
        Guid? cashRegisterId, 
        Guid? branchId, 
        Guid? shiftId, 
        string? userId, 
        PDV.Domain.Enums.PaymentMethodType? paymentMethod, 
        string? series, 
        int? folio, 
        bool? isPaid, 
        bool? isCancelled, 
        bool? isInvoiced, 
        string? invoiceId, 
        DateTime? startDate, 
        DateTime? endDate, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales.AsQueryable();

        if (clientId.HasValue) query = query.Where(s => s.ClientId == clientId.Value);
        if (cashRegisterId.HasValue) query = query.Where(s => s.CashRegisterId == cashRegisterId.Value);
        if (branchId.HasValue) query = query.Where(s => s.BranchId == branchId.Value);
        if (shiftId.HasValue) query = query.Where(s => s.ShiftId == shiftId.Value);
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(s => s.UserId == userId);
        if (paymentMethod.HasValue) query = query.Where(s => s.PaymentMethod == paymentMethod.Value);
        if (!string.IsNullOrWhiteSpace(series)) query = query.Where(s => s.Series == series);
        if (folio.HasValue) query = query.Where(s => s.Folio == folio.Value);
        if (isPaid.HasValue) query = query.Where(s => s.IsPaid == isPaid.Value);
        if (isCancelled.HasValue) query = query.Where(s => s.IsCancelled == isCancelled.Value);
        if (isInvoiced.HasValue) query = query.Where(s => s.IsInvoiced == isInvoiced.Value);
        if (!string.IsNullOrWhiteSpace(invoiceId)) query = query.Where(s => s.InvoiceId == invoiceId);
        
        if (startDate.HasValue) query = query.Where(s => s.Date >= startDate.Value);
        if (endDate.HasValue) query = query.Where(s => s.Date <= endDate.Value);

        return await query.ToListAsync(cancellationToken);
    }
}
