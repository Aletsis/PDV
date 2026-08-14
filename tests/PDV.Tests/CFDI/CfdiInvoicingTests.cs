using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Commands.CreateInvoice;
using PDV.Application.Features.Sales.Commands.CancelInvoice;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Domain.ValueObjects;
using PDV.Infrastructure.Common;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Interceptors;
using PDV.Infrastructure.Repositories;
using Xunit;
using Moq;

namespace PDV.Tests.CFDI;

public class CfdiInvoicingTests
{
    private (byte[] certBytes, byte[] keyBytes, string serialNumber) GenerateTestCredentials()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Empresa de Prueba SA de CV, OID.2.5.4.45=AAA010101AAA",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        byte[] certBytes = certificate.Export(X509ContentType.Cert);
        byte[] keyBytes = rsa.ExportEncryptedPkcs8PrivateKey(
            "password123",
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100));

        // Let's use a 20-character hex ASCII-like string for SAT
        return (certBytes, keyBytes, "3030303031303030303530303030303030303031");
    }

    [Fact]
    public void CsdCertificateService_ExtractMetadata_ValidCertificate_ReturnsCorrectMetadata()
    {
        // Arrange
        var service = new CsdCertificateService();
        var (certBytes, _, _) = GenerateTestCredentials();

        // Act
        var metadata = service.ExtractMetadata(certBytes);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("AAA010101AAA", metadata.RfcEmisor);
        Assert.Equal("Empresa de Prueba SA de CV", metadata.CompanyName);
        Assert.True(metadata.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void CsdCertificateService_SignCadenaOriginal_ValidData_ReturnsSignature()
    {
        // Arrange
        var service = new CsdCertificateService();
        var (_, keyBytes, _) = GenerateTestCredentials();
        string cadenaOriginal = "||4.0|TEST|...|";

        // Act
        string signature = service.SignCadenaOriginal(cadenaOriginal, keyBytes, "password123");

        // Assert
        Assert.False(string.IsNullOrEmpty(signature));
        Assert.True(signature.Length > 20); // Valid base64 signature length
    }

    [Fact]
    public async Task CfdiXmlGenerator_GenerateCfdi40Xml_ValidInvoice_GeneratesXmlWithCorrectNodes()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_CfdiXml_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);
        var generator = new CfdiXmlGenerator(context);

        var (certBytes, _, _) = GenerateTestCredentials();
        var config = new SystemConfiguration("Empresa de Prueba SA de CV", "AAA010101AAA", "601");
        config.UpdateInvoiceSettings("30303030313030303035", DateTime.UtcNow.AddDays(100), "http://localhost", "user", "key", certBytes, new byte[1], "pwd");
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        config.SetFiscalAddress(address);
        context.SystemConfigurations.Add(config);

        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

        var client = new Client("CLI001", "Cliente Test", "XAXX010101000", "5559876543", "test@test.com");
        client.UpdateAddress(address);
        client.UpdateFiscalProfile("616", "06000");
        context.Clients.Add(client);

        var product = new Product("Producto Test", "P-001", 100m, SaleType.Piece, TaxRateType.Rate16, "Cat");
        context.Products.Add(product);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "user", 1000m);
        context.Shifts.Add(shift);

        await context.SaveChangesAsync();

        var saleNumber = SaleNumber.Create("T-0001");
        var sale = new Sale(
            saleNumber: saleNumber,
            paymentMethod: PaymentMethodType.Cash,
            userId: "user",
            shiftId: shift.Id,
            series: "A",
            folio: 1,
            clientId: client.Id,
            cashRegisterId: cashRegister.Id);
        sale.SetBranch(branch.Id);

        var saleItem = new SaleItem(product, 2, 16m);
        sale.AddItem(saleItem);
        sale.MarkAsPaid();

        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var taxBreakdowns = new List<TaxBreakdown> { new TaxBreakdown(16m, 200m, 32m, false) };
        var invoice = Invoice.CreateCustomerInvoice(
            branchId: branch.Id,
            series: "A",
            folio: "1",
            saleId: sale.Id,
            clientId: client.Id,
            receiverTaxId: "XAXX010101000",
            receiverName: "Cliente Test",
            cfdiUsage: CfdiUsage.GeneralExpense,
            subtotal: 200m,
            taxBreakdowns: taxBreakdowns,
            receiverFiscalRegime: "616",
            receiverZipCode: "06000"
        );
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        // Act
        string xml = generator.GenerateCfdi40Xml(invoice, config, "PUE", "01");

        // Assert
        Assert.False(string.IsNullOrEmpty(xml));
        Assert.Contains("<cfdi:Comprobante", xml);
        Assert.Contains("Version=\"4.0\"", xml);
        Assert.Contains("Rfc=\"AAA010101AAA\"", xml);
        Assert.Contains("Rfc=\"XAXX010101000\"", xml);
    }

    [Fact]
    public async Task CreateInvoiceCommand_Handle_ValidCommand_GeneratesAndStampsInvoice()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_CreateInvoice_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);

        var (certBytes, keyBytes, _) = GenerateTestCredentials();
        
        var config = new SystemConfiguration("Empresa de Prueba SA de CV", "AAA010101AAA", "601");
        config.UpdateInvoiceSettings("30303030313030303035", DateTime.UtcNow.AddDays(10), "http://localhost", "user", "key", certBytes, keyBytes, "password123");
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        config.SetFiscalAddress(address);
        context.SystemConfigurations.Add(config);

        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

        var client = new Client("CLI001", "Cliente Test", "XAXX010101000", "5559876543", "test@test.com");
        client.UpdateAddress(address);
        client.UpdateFiscalProfile("616", "06000");
        context.Clients.Add(client);

        var product = new Product("Producto Test", "P-001", 100m, SaleType.Piece, TaxRateType.Rate16, "Cat");
        context.Products.Add(product);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "user", 1000m);
        context.Shifts.Add(shift);

        var folioSequence = new FolioSequence(branch.Id, InvoiceType.Customer, "A", 6);
        context.FolioSequences.Add(folioSequence);

        await context.SaveChangesAsync();

        var saleNumber = SaleNumber.Create("T-0001");
        var sale = new Sale(
            saleNumber: saleNumber,
            paymentMethod: PaymentMethodType.Cash,
            userId: "user",
            shiftId: shift.Id,
            series: "A",
            folio: 1,
            clientId: client.Id,
            cashRegisterId: cashRegister.Id);
        sale.SetBranch(branch.Id);

        var saleItem = new SaleItem(product, 2, 16m);
        sale.AddItem(saleItem);
        sale.MarkAsPaid();

        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var saleRepository = new SaleRepository(context);
        var csdCertificateService = new CsdCertificateService();
        var cfdiXmlGenerator = new CfdiXmlGenerator(context);
        var pacService = new MockPacService();

        var mockComercialSyncService = new Mock<IComercialApiSyncService>();
        var handler = new CreateInvoiceCommandHandler(
            saleRepository,
            context,
            csdCertificateService,
            cfdiXmlGenerator,
            pacService,
            mockComercialSyncService.Object);

        var command = new CreateInvoiceCommand
        {
            SaleId = sale.Id,
            IsGlobal = false,
            ClientId = client.Id,
            UsoCfdi = "G03",
            MetodoPago = "PUE",
            FormaPago = "01",
            TaxRate = 0.16m,
            ReceiverFiscalRegime = "616",
            ReceiverZipCode = "06000"
        };

        // Act
        var invoiceId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, invoiceId);
        
        var invoice = await context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.Stamped, invoice!.Status);
        Assert.NotNull(invoice.Uuid);
        Assert.NotEmpty(invoice.SelloDigitalSAT);
        Assert.Equal("XAXX010101000", invoice.ReceiverTaxId);
    }

    [Fact]
    public async Task CancelInvoiceCommand_Handle_StampedInvoice_CancelsInvoiceAtSat()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PDV_CancelInvoice_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);

        var config = new SystemConfiguration("Empresa de Prueba SA de CV", "AAA010101AAA", "601");
        config.UpdateInvoiceSettings("30303030313030303035", DateTime.UtcNow.AddDays(10), "http://localhost", "user", "key");
        var address = Address.Create("Calle Falsa 123", "Centro", "CDMX", "06000", "México");
        config.SetFiscalAddress(address);
        context.SystemConfigurations.Add(config);

        var branch = new Branch("Sucursal Centro", "SC001", address, "5551234567");
        context.Branches.Add(branch);

        var client = new Client("CLI001", "Cliente Test", "XAXX010101000", "5559876543", "test@test.com");
        client.UpdateAddress(address);
        context.Clients.Add(client);

        var product = new Product("Producto Test", "P-001", 100m, SaleType.Piece, TaxRateType.Rate16, "Cat");
        context.Products.Add(product);

        var cashRegister = new CashRegister("Caja 1", "CR01", branch.Id);
        context.CashRegisters.Add(cashRegister);

        var shift = new Shift(cashRegister.Id, "user", 1000m);
        context.Shifts.Add(shift);

        await context.SaveChangesAsync();

        var saleNumber = SaleNumber.Create("T-0001");
        var sale = new Sale(
            saleNumber: saleNumber,
            paymentMethod: PaymentMethodType.Cash,
            userId: "user",
            shiftId: shift.Id,
            series: "A",
            folio: 1,
            clientId: client.Id,
            cashRegisterId: cashRegister.Id);
        sale.SetBranch(branch.Id);

        var saleItem = new SaleItem(product, 2, 16m);
        sale.AddItem(saleItem);
        sale.MarkAsPaid();

        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var taxBreakdowns = new List<TaxBreakdown> { new TaxBreakdown(16m, 200m, 32m, false) };
        var invoice = Invoice.CreateCustomerInvoice(
            branchId: branch.Id,
            series: "A",
            folio: "1",
            saleId: sale.Id,
            clientId: client.Id,
            receiverTaxId: "XAXX010101000",
            receiverName: "Cliente Test",
            cfdiUsage: CfdiUsage.GeneralExpense,
            subtotal: 200m,
            taxBreakdowns: taxBreakdowns,
            receiverFiscalRegime: "616",
            receiverZipCode: "06000"
        );
        invoice.Stamp("UUID-123-456", DateTime.UtcNow, "selloEmisor", "selloSat", "30303030313030303035", "certSat", "cadena");
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var pacService = new MockPacService();
        var mockComercialSyncService = new Mock<IComercialApiSyncService>();
        var handler = new CancelInvoiceCommandHandler(context, pacService, mockComercialSyncService.Object);

        var command = new CancelInvoiceCommand
        {
            InvoiceId = invoice.Id,
            Motif = SatCancellationMotif.ErrorWithoutRelation,
            Reason = "Test cancellation reason",
            SubstituteUuid = null
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var updatedInvoice = await context.Invoices.FindAsync(invoice.Id);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(InvoiceStatus.CancelledAtSat, updatedInvoice!.Status);
        Assert.NotNull(updatedInvoice.CancelledAt);
    }
}
