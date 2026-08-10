using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.CashRegisters.Commands.OpenCashDrawer;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Infrastructure.Persistence;
using Xunit;

namespace PDV.Tests.HardwareAgent;

public class OpenCashDrawerCommandTests
{
    [Fact]
    public async Task Handle_WhenEmptyCashRegisterId_ReturnsFalse()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Drawer_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);
        var mockPrinter = new Mock<IEscPosPrinter>();
        var handler = new OpenCashDrawerCommandHandler(context, mockPrinter.Object);

        var command = new OpenCashDrawerCommand(Guid.Empty);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result);
        mockPrinter.Verify(p => p.OpenDrawerAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCashRegisterHasPrinter_CallsOpenDrawerAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_Drawer_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);

        var branchId = Guid.NewGuid();
        var printer = new Printer("Caja 1 Printer", PrinterConnectionType.Usb, 1252, 384, devicePath: "EPSON_TM_T20", port: 9100);
        var register = new CashRegister("Caja 1", "Piso", branchId);
        register.AssignPrinter(printer.Id);

        context.Printers.Add(printer);
        context.CashRegisters.Add(register);
        await context.SaveChangesAsync();

        var mockPrinter = new Mock<IEscPosPrinter>();
        var handler = new OpenCashDrawerCommandHandler(context, mockPrinter.Object);

        var command = new OpenCashDrawerCommand(register.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result);
        mockPrinter.Verify(p => p.OpenDrawerAsync(
            It.Is<string>(uri => uri == "usb://EPSON_TM_T20"),
            It.Is<int>(port => port == 9100),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
