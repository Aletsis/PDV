using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Commands.ReconcileCashCut;
using PDV.Application.Features.Sales.Dtos;
using PDV.Application.Features.Sales.Queries.GetReconciliationsList;
using PDV.Application.Features.Sales.Queries.GetShiftReconciliationDetail;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace PDV.Tests.Sales;

public class CashCutReconciliationTests
{
    private (AppDbContext context, Mock<ICurrentUserService> currentUserServiceMock, Mock<IIdentityService> identityServiceMock) CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Reconciliation_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new DomainEventsInterceptor())
            .Options;

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns("supervisor-01");

        var identityServiceMock = new Mock<IIdentityService>();
        identityServiceMock.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserSyncDataDto>
            {
                new UserSyncDataDto { Id = "cajero-01", UserName = "cajero01", FullName = "Cajera Lupita", Email = "cajera@pdv.local" },
                new UserSyncDataDto { Id = "supervisor-01", UserName = "super01", FullName = "Encargada Laura", Email = "super@pdv.local" }
            });

        var context = new AppDbContext(options, currentUserService: currentUserServiceMock.Object);

        return (context, currentUserServiceMock, identityServiceMock);
    }

    [Fact]
    public async Task ReconcileCashCut_WhenAmountsMatch_ReturnsBalancedStatus()
    {
        // Arrange
        var (context, currentUserService, _) = CreateContext();

        var branch = new Branch("Sucursal 1", "S01", Address.Create("Calle 1", "Col", "CDMX", "00000", "MX"), "1234567890");
        context.Branches.Add(branch);

        var cashRegister = new CashRegister("Caja 1", "CR-01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        // Turno con $500 iniciales
        var shift = new Shift(cashRegister.Id, "cajero-01", 500m);
        context.Shifts.Add(shift);

        var product1 = new Product("Prod 1", "P01", 200m, SaleType.Piece, TaxRateType.Exempt, "Cat");
        var product2 = new Product("Prod 2", "P02", 300m, SaleType.Piece, TaxRateType.Exempt, "Cat");
        context.Products.AddRange(product1, product2);

        // Venta en efectivo de $200
        var saleNumber1 = SaleNumber.Create("V-001");
        var saleCash = new Sale(saleNumber1, PaymentMethodType.Cash, "cajero-01", shift.Id, "A", 1, null, cashRegister.Id);
        saleCash.AddItem(new SaleItem(product1, 1, 0m, true));
        saleCash.MarkAsPaid();
        context.Sales.Add(saleCash);

        // Venta con tarjeta de $300
        var saleNumber2 = SaleNumber.Create("V-002");
        var saleCard = new Sale(saleNumber2, PaymentMethodType.CreditCard, "cajero-01", shift.Id, "A", 2, null, cashRegister.Id);
        saleCard.AddItem(new SaleItem(product2, 1, 0m, true));
        saleCard.MarkAsPaid();
        context.Sales.Add(saleCard);

        // Morralla de $100
        var inflowDenoms = new List<CashDenomination> { new(DenominationType.Bill_100, 1) };
        var inflow = new CashCollection(shift.Id, cashRegister.Id, "cajero-01", inflowDenoms, "Morralla cambio", CashCollectionType.Morralla);
        context.CashCollections.Add(inflow);

        // Recolección de $150
        var outflowDenoms = new List<CashDenomination> { new(DenominationType.Bill_100, 1), new(DenominationType.Bill_50, 1) };
        var outflow = new CashCollection(shift.Id, cashRegister.Id, "cajero-01", outflowDenoms, "Retiro parcial", CashCollectionType.Recoleccion);
        context.CashCollections.Add(outflow);

        // Cerrar turno formalmente
        shift.Close(
            endTime: DateTime.UtcNow,
            totalCashSales: 200m,
            totalCashReturns: 0m,
            totalInflows: 100m,
            totalOutflows: 150m,
            paymentMethodTotals: new List<PaymentMethodBreakdown>
            {
                new(PaymentMethodType.Cash, 200m),
                new(PaymentMethodType.CreditCard, 300m)
            },
            salesTaxTotals: new List<TaxBreakdown>(),
            returnsTaxTotals: new List<TaxBreakdown>()
        );

        await context.SaveChangesAsync();

        // Esperado en efectivo: 500 (inicial) + 200 (efectivo) + 100 (morralla) - 150 (recolección) = 650
        // Esperado en tarjeta/váuchers: 300

        var handler = new ReconcileCashCutCommandHandler(context, currentUserService.Object);
        var command = new ReconcileCashCutCommand(
            ShiftId: shift.Id,
            DeliveredCash: 650m,
            DeliveredCardVouchers: 300m,
            Notes: "Corte cuadrado conforme a sistema"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ReconciliationStatus.Balanced, result.Status);
        Assert.True(result.IsBalanced);
        Assert.Equal(650m, result.ExpectedCash);
        Assert.Equal(650m, result.DeliveredCash);
        Assert.Equal(0m, result.CashDifference);
        Assert.Equal(300m, result.ExpectedCardVouchers);
        Assert.Equal(300m, result.DeliveredCardVouchers);
        Assert.Equal(0m, result.CardVouchersDifference);

        var savedShift = await context.Shifts.FindAsync(shift.Id);
        Assert.True(savedShift!.IsReconciled);

        var savedRec = await context.CashCutReconciliations.FirstOrDefaultAsync(r => r.ShiftId == shift.Id);
        Assert.NotNull(savedRec);
        Assert.Equal(ReconciliationStatus.Balanced, savedRec.Status);
        Assert.Equal("supervisor-01", savedRec.ReconciledByUserId);
    }

    [Fact]
    public async Task ReconcileCashCut_WhenCashIsShort_ReturnsCashShortageStatus()
    {
        // Arrange
        var (context, currentUserService, _) = CreateContext();

        var branch = new Branch("Sucursal 1", "S01", Address.Create("Calle 1", "Col", "CDMX", "00000", "MX"), "1234567890");
        context.Branches.Add(branch);
        var cashRegister = new CashRegister("Caja 1", "CR-01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "cajero-01", 1000m);
        context.Shifts.Add(shift);

        shift.Close(
            endTime: DateTime.UtcNow,
            totalCashSales: 0m,
            totalCashReturns: 0m,
            totalInflows: 0m,
            totalOutflows: 0m,
            paymentMethodTotals: new List<PaymentMethodBreakdown>(),
            salesTaxTotals: new List<TaxBreakdown>(),
            returnsTaxTotals: new List<TaxBreakdown>()
        );
        await context.SaveChangesAsync();

        var handler = new ReconcileCashCutCommandHandler(context, currentUserService.Object);
        var command = new ReconcileCashCutCommand(
            ShiftId: shift.Id,
            DeliveredCash: 950m, // Faltante de $50
            DeliveredCardVouchers: 0m,
            Notes: "Faltante en caja"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ReconciliationStatus.CashShortage, result.Status);
        Assert.False(result.IsBalanced);
        Assert.Equal(-50m, result.CashDifference);
        Assert.Contains("FALTANTE en efectivo", result.Message);
    }

    [Fact]
    public async Task ReconcileCashCut_WhenShiftIsOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        var (context, currentUserService, _) = CreateContext();

        var branch = new Branch("Sucursal 1", "S01", Address.Create("Calle 1", "Col", "CDMX", "00000", "MX"), "1234567890");
        context.Branches.Add(branch);
        var cashRegister = new CashRegister("Caja 1", "CR-01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "cajero-01", 1000m); // ShiftStatus.Open
        context.Shifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new ReconcileCashCutCommandHandler(context, currentUserService.Object);
        var command = new ReconcileCashCutCommand(shift.Id, 1000m, 0m);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task ReconcileCashCut_WhenAlreadyReconciled_ThrowsInvalidOperationException()
    {
        // Arrange
        var (context, currentUserService, _) = CreateContext();

        var branch = new Branch("Sucursal 1", "S01", Address.Create("Calle 1", "Col", "CDMX", "00000", "MX"), "1234567890");
        context.Branches.Add(branch);
        var cashRegister = new CashRegister("Caja 1", "CR-01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "cajero-01", 1000m);
        context.Shifts.Add(shift);
        shift.Close(DateTime.UtcNow, 0m, 0m, 0m, 0m, new List<PaymentMethodBreakdown>(), new List<TaxBreakdown>(), new List<TaxBreakdown>());
        shift.MarkAsReconciled();
        await context.SaveChangesAsync();

        var handler = new ReconcileCashCutCommandHandler(context, currentUserService.Object);
        var command = new ReconcileCashCutCommand(shift.Id, 1000m, 0m);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }
}
