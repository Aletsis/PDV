using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Invoices.Commands.PrintInvoiceTicket;
using PDV.Application.Features.Sales.Commands.ReturnSale;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Persistence;
using Xunit;

namespace PDV.Tests.HardwareAgent;

public class PrintCommandsTests
{
    [Fact]
    public async Task PrintInvoiceTicketCommand_SendsPrintJobWithAutoCutAndCopies()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Print_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);

        var branchId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var invoice = Invoice.CreateCustomerInvoice(
            branchId: branchId,
            series: "FAC",
            folio: "001",
            saleId: saleId,
            clientId: clientId,
            receiverTaxId: "XAXX010101000",
            receiverName: "PUBLICO EN GENERAL",
            cfdiUsage: CfdiUsage.GeneralExpense,
            subtotal: 1000m,
            taxBreakdowns: new List<TaxBreakdown> { new TaxBreakdown(0.16m, 1000m, 160m, false) }
        );

        var printer = new Printer("Fiscal Printer", PrinterConnectionType.Network, 1252, 384, ipAddress: "192.168.1.50", port: 9100);
        var register = new CashRegister("Caja Principal", "Piso", branchId);
        register.AssignPrinter(printer.Id);

        var config = new SystemConfiguration("Mi Empresa", "XAXX010101000", "601");
        config.UpdateTicketSettings(2);

        context.Invoices.Add(invoice);
        context.Printers.Add(printer);
        context.CashRegisters.Add(register);
        context.SystemConfigurations.Add(config);
        await context.SaveChangesAsync();

        var mockTicketGenerator = new Mock<ITicketGenerator>();
        var mockPrinter = new Mock<IEscPosPrinter>();

        mockTicketGenerator.Setup(t => t.GenerateInvoiceTicketAsync(invoice.Id, It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync("=== FACTURA FISCAL ===");

        var handler = new PrintInvoiceTicketCommandHandler(
            context,
            mockTicketGenerator.Object,
            mockPrinter.Object);

        await handler.Handle(new PrintInvoiceTicketCommand(invoice.Id, register.Id), CancellationToken.None);

        mockPrinter.Verify(p => p.PrintJobAsync(
            "192.168.1.50",
            9100,
            "=== FACTURA FISCAL ===",
            true,
            true,
            false,
            false,
            2,
            1252,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrintReturnTicketCommand_WhenCashRefund_EnablesOpenDrawerAfter()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Print_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);

        var branchId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        var returnEntity = new Return("Defectuoso", RefundMethod.Cash, "USER1", shiftId);

        var printer = new Printer("USB Printer", PrinterConnectionType.Usb, 1252, 384, devicePath: "EPSON_TM_T20", port: 9100);
        var register = new CashRegister("Caja Principal", "Piso", branchId);
        register.AssignPrinter(printer.Id);

        var config = new SystemConfiguration("Mi Empresa", "XAXX010101000", "601");
        config.UpdateTicketSettings(2);

        context.Returns.Add(returnEntity);
        context.Printers.Add(printer);
        context.CashRegisters.Add(register);
        context.SystemConfigurations.Add(config);
        await context.SaveChangesAsync();

        var mockTicketGenerator = new Mock<ITicketGenerator>();
        var mockPrinter = new Mock<IEscPosPrinter>();

        mockTicketGenerator.Setup(t => t.GenerateReturnTicketAsync(returnEntity.Id, It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync("=== TICKET DEVOLUCION ===");

        var handler = new PrintReturnTicketCommandHandler(
            context,
            mockTicketGenerator.Object,
            mockPrinter.Object);

        await handler.Handle(new PrintReturnTicketCommand(returnEntity.Id, register.Id), CancellationToken.None);

        mockPrinter.Verify(p => p.PrintJobAsync(
            "usb://EPSON_TM_T20",
            9100,
            "=== TICKET DEVOLUCION ===",
            true,
            true,
            false,
            true, // OpenDrawerAfter = true porque el reembolso fue en efectivo
            2,
            1252,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
