using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Order?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Client)
            .Include(o => o.DeliveryZone)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders.ToListAsync(cancellationToken);
    }

    public Task<int> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(order);
        return Task.FromResult(0);
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Update(order);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.SoftDelete("system");
        _context.Orders.Update(order);
        await Task.CompletedTask;
    }

    public async Task<List<Order>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.ClientId == clientId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByRouteIdAsync(Guid routeId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.DeliveryRouteId == routeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByDeliveryManIdAsync(string deliveryManId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.DeliveryManId == deliveryManId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByTakenByIdAsync(string takenById, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.TakenById == takenById)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByFilledByIdAsync(string filledById, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.FilledById == filledById)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByCapturedByIdAsync(string capturedById, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.CapturedById == capturedById)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.Orders
            .Where(o => o.OrderDate.Date == targetDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByPaymentMethodAsync(PaymentMethodType paymentMethod, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.PaymentMethod == paymentMethod)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByInvoicedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.IsInvoiceRequested)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByNotInvoicedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => !o.IsInvoiceRequested)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByAuthorizedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.AuthorizedBySupervisorId != null)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByCashRegisterIdAsync(Guid cashRegisterId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.CashRegisterId == cashRegisterId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.BranchId == branchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextFolioAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var maxFolio = await _context.Orders
            .IgnoreQueryFilters()
            .Where(o => o.BranchId == branchId)
            .Select(o => (int?)o.Folio)
            .MaxAsync(cancellationToken);

        return (maxFolio ?? 0) + 1;
    }

    public async Task<Order?> GetByFolioAsync(Guid? cashRegisterId, string series, int folio, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o => o.CashRegisterId == cashRegisterId && o.Series == series && o.Folio == folio, cancellationToken);
    }

    public async Task<List<Order>> GetByCriteriaAsync(
        Guid? clientId, 
        Guid? cashRegisterId, 
        Guid? branchId, 
        string? series, 
        int? folio, 
        Guid? routeId, 
        string? deliveryManId, 
        string? takenById, 
        string? filledById, 
        string? capturedById, 
        DateTime? startDate, 
        DateTime? endDate, 
        OrderStatus? status, 
        PaymentMethodType? paymentMethod, 
        bool? isInvoiceRequested, 
        bool? isAuthorized, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.AsQueryable();

        if (clientId.HasValue) query = query.Where(o => o.ClientId == clientId.Value);
        if (cashRegisterId.HasValue) query = query.Where(o => o.CashRegisterId == cashRegisterId.Value);
        if (branchId.HasValue) query = query.Where(o => o.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(series)) query = query.Where(o => o.Series == series);
        if (folio.HasValue) query = query.Where(o => o.Folio == folio.Value);
        if (routeId.HasValue) query = query.Where(o => o.DeliveryRouteId == routeId.Value);
        if (!string.IsNullOrWhiteSpace(deliveryManId)) query = query.Where(o => o.DeliveryManId == deliveryManId);
        if (!string.IsNullOrWhiteSpace(takenById)) query = query.Where(o => o.TakenById == takenById);
        if (!string.IsNullOrWhiteSpace(filledById)) query = query.Where(o => o.FilledById == filledById);
        if (!string.IsNullOrWhiteSpace(capturedById)) query = query.Where(o => o.CapturedById == capturedById);
        
        if (startDate.HasValue) query = query.Where(o => o.OrderDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(o => o.OrderDate <= endDate.Value);
        
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (paymentMethod.HasValue) query = query.Where(o => o.PaymentMethod == paymentMethod.Value);
        if (isInvoiceRequested.HasValue) query = query.Where(o => o.IsInvoiceRequested == isInvoiceRequested.Value);
        if (isAuthorized.HasValue)
        {
            if (isAuthorized.Value)
                query = query.Where(o => o.AuthorizedBySupervisorId != null);
            else
                query = query.Where(o => o.AuthorizedBySupervisorId == null);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
