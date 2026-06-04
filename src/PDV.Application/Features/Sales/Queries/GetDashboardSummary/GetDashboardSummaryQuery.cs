using MediatR;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Sales.Queries.GetDashboardSummary;

// ── DTOs de respuesta ─────────────────────────────────────────────────────

public class DashboardSummaryDto
{
    public decimal TotalSalesToday { get; set; }
    public int TransactionCountToday { get; set; }
    public int ActiveShiftsCount { get; set; }
    public int LowStockCount { get; set; }
    public List<HourlySaleDto> SalesByHour { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
    public List<ActiveShiftDto> ActiveShifts { get; set; } = new();
}

public class HourlySaleDto
{
    public string Hour { get; set; } = string.Empty;   // "08:00", "09:00", ...
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class TopProductDto
{
    public string Name { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class ActiveShiftDto
{
    public Guid ShiftId { get; set; }
    public string CashRegisterName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public decimal InitialCash { get; set; }
    public DateTime StartTime { get; set; }
    public decimal TotalSales { get; set; }
}

// ── Query / Handler ───────────────────────────────────────────────────────

public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        // ── Ventas del día ────────────────────────────────────────────────
        var todaySales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Date >= todayUtc && s.Date < tomorrowUtc && !s.IsCancelled)
            .ToListAsync(cancellationToken);

        var totalSalesToday = todaySales.Sum(s => s.TotalAmount);
        var transactionCount = todaySales.Count;

        // ── Turnos activos ────────────────────────────────────────────────
        var activeShiftsRaw = await _context.Shifts
            .AsNoTracking()
            .Include(s => s.CashRegister)
            .Where(s => s.Status == ShiftStatus.Open)
            .ToListAsync(cancellationToken);

        var usersDict = new Dictionary<string, string>();
        try
        {
            var users = await _identityService.GetUsersAsync(cancellationToken);
            usersDict = users.ToDictionary(u => u.Id, u => u.FullName);
        }
        catch
        {
            // Offline/error fallback
        }

        var activeShiftDtos = activeShiftsRaw.Select(s =>
        {
            var shiftSales = todaySales
                .Where(sale => sale.ShiftId == s.Id)
                .Sum(sale => sale.TotalAmount);

            var cashierName = "Desconocido";
            if (!string.IsNullOrEmpty(s.UserId))
            {
                if (usersDict.TryGetValue(s.UserId, out var name))
                {
                    cashierName = name;
                }
                else
                {
                    cashierName = s.UserId;
                }
            }

            return new ActiveShiftDto
            {
                ShiftId = s.Id,
                CashRegisterName = s.CashRegister?.Name ?? "Sin nombre",
                UserId = s.UserId,
                CashierName = cashierName,
                InitialCash = s.InitialCash,
                StartTime = s.StartTime,
                TotalSales = shiftSales
            };
        }).ToList();

        // ── Bajo stock ────────────────────────────────────────────────────
        var lowStockCount = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.MinStock > 0 && p.Stock <= p.MinStock)
            .CountAsync(cancellationToken);

        // ── Ventas por hora (últimas 8 horas) ─────────────────────────────
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-8);

        var salesByHour = todaySales
            .Where(s => s.Date >= cutoff)
            .GroupBy(s => s.Date.Hour)
            .Select(g => new HourlySaleDto
            {
                Hour = $"{g.Key:D2}:00",
                Total = g.Sum(s => s.TotalAmount),
                Count = g.Count()
            })
            .OrderBy(h => h.Hour)
            .ToList();

        // Rellenar horas vacías en el rango
        for (int i = 0; i < 8; i++)
        {
            var hourStr = $"{((now.Hour - 7 + i + 24) % 24):D2}:00";
            if (!salesByHour.Any(h => h.Hour == hourStr))
                salesByHour.Add(new HourlySaleDto { Hour = hourStr, Total = 0, Count = 0 });
        }
        salesByHour = salesByHour.OrderBy(h => h.Hour).ToList();

        // ── Top 5 productos del día ───────────────────────────────────────
        var topProducts = todaySales
            .SelectMany(s => s.Items.Select(i => new
            {
                i.ProductName,
                i.Quantity,
                Revenue = i.Quantity * i.UnitPrice
            }))
            .GroupBy(x => x.ProductName)
            .Select(g => new TopProductDto
            {
                Name = g.Key,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Revenue)
            })
            .OrderByDescending(p => p.Revenue)
            .Take(5)
            .ToList();

        return new DashboardSummaryDto
        {
            TotalSalesToday = totalSalesToday,
            TransactionCountToday = transactionCount,
            ActiveShiftsCount = activeShiftsRaw.Count,
            LowStockCount = lowStockCount,
            SalesByHour = salesByHour,
            TopProducts = topProducts,
            ActiveShifts = activeShiftDtos
        };
    }
}
