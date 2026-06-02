using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Persistence;

namespace PDV.Infrastructure.Repositories;

/// <summary>
/// Implementación del repositorio de Product usando EF Core
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Code == code || p.Plu == code || p.Barcode == code, cancellationToken);
    }

    public async Task<List<Product>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => EF.Functions.Like(p.Name, $"%{name}%"))
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetByPluAsync(string plu, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Plu == plu, cancellationToken);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Barcode == barcode, cancellationToken);
    }

    public async Task<List<Product>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.Description != null && EF.Functions.Like(p.Description, $"%{description}%"))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.Category == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> GetBySaleTypeAsync(SaleType saleType, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.SaleType == saleType)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> GetByTaxRateAsync(TaxRateType taxRate, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.TaxRate == taxRate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> GetAllInactiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => !p.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        // Retorna productos de esa sucursal o globales (BranchId == null)
        return await _context.Products
            .Where(p => p.BranchId == branchId || p.BranchId == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> GetByCriteriaAsync(
        string? name, 
        string? code, 
        string? plu, 
        string? barcode, 
        string? category, 
        SaleType? saleType, 
        TaxRateType? taxRate, 
        bool? isActive, 
        Guid? branchId, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{name}%"));

        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(p => p.Code == code);

        if (!string.IsNullOrWhiteSpace(plu))
            query = query.Where(p => p.Plu == plu);

        if (!string.IsNullOrWhiteSpace(barcode))
            query = query.Where(p => p.Barcode == barcode);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        if (saleType.HasValue)
            query = query.Where(p => p.SaleType == saleType.Value);

        if (taxRate.HasValue)
            query = query.Where(p => p.TaxRate == taxRate.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId.Value || p.BranchId == null);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products.ToListAsync(cancellationToken);
    }

    public async Task<int> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Add(product);
        return await Task.FromResult(0);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Update(product);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Remove(product);
        await Task.CompletedTask;
    }
}
