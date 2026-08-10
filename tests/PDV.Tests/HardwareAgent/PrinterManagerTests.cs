using Microsoft.Extensions.Logging;
using Moq;
using PDV.Domain.Enums;
using PDV.HardwareAgent.Contracts.Enums;
using PDV.HardwareAgent.Contracts.Models;
using PDV.HardwareAgent.Profiles;
using PDV.HardwareAgent.Services;
using PDV.HardwareAgent.Transports;

namespace PDV.Tests.HardwareAgent;

public class PrinterManagerTests
{
    private readonly Mock<ITransportFactory> _transportFactoryMock = new();
    private readonly Mock<IPrinterTransport> _transportMock = new();
    private readonly IPrinterProfileFactory _profileFactory = new PrinterProfileFactory();
    private readonly Mock<ILogger<PrinterManager>> _loggerMock = new();

    private readonly PrinterManager _manager;

    public PrinterManagerTests()
    {
        _transportMock.Setup(t => t.ConnectionType).Returns(PrinterConnectionType.Network);
        _transportMock.Setup(t => t.TargetEndpoint).Returns("tcp://127.0.0.1:9100");
        _transportMock.Setup(t => t.CheckAvailabilityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _transportMock.Setup(t => t.SendBytesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _transportFactoryMock
            .Setup(f => f.CreateTransport(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(_transportMock.Object);

        _manager = new PrinterManager(_transportFactoryMock.Object, _profileFactory, _loggerMock.Object);
    }

    [Fact]
    public async Task PrintJobAsync_EmptyTarget_ShouldReturnValidationError()
    {
        var request = new PrintJobRequest
        {
            Target = "",
            Data = "Hello World"
        };

        var result = await _manager.PrintJobAsync(request);

        Assert.False(result.Success);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task PrintJobAsync_ValidTextJob_ShouldSucceedOnFirstAttempt()
    {
        var request = new PrintJobRequest
        {
            Target = "tcp://192.168.1.50:9100",
            ContentType = PrintJobContentType.Text,
            Data = "TICKET #1234\nTotal: $200.00",
            AutoCut = true
        };

        var result = await _manager.PrintJobAsync(request);

        Assert.True(result.Success);
        Assert.Equal(1, result.Attempts);
        _transportMock.Verify(t => t.SendBytesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrintJobAsync_TransientFailure_ShouldRetryAndSucceed()
    {
        int callCount = 0;
        _transportMock
            .Setup(t => t.SendBytesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount < 3) throw new IOException("Temporary network glitch");
                return Task.CompletedTask;
            });

        var request = new PrintJobRequest
        {
            Target = "tcp://192.168.1.50:9100",
            ContentType = PrintJobContentType.Text,
            Data = "TICKET #1234",
            MaxRetries = 3
        };

        var result = await _manager.PrintJobAsync(request);

        Assert.True(result.Success);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task PrintJobAsync_PermanentFailure_ShouldReturnTransmissionErrorAfterMaxRetries()
    {
        _transportMock
            .Setup(t => t.SendBytesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Throws(new IOException("Printer is completely offline"));

        var request = new PrintJobRequest
        {
            Target = "tcp://192.168.1.50:9100",
            ContentType = PrintJobContentType.Text,
            Data = "TICKET #1234",
            MaxRetries = 2
        };

        var result = await _manager.PrintJobAsync(request);

        Assert.False(result.Success);
        Assert.Equal("TRANSMISSION_ERROR", result.ErrorCode);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task CheckStatusAsync_WhenOnline_ShouldReturnOnlineStatus()
    {
        _transportMock.Setup(t => t.CheckAvailabilityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var status = await _manager.CheckStatusAsync("tcp://192.168.1.50:9100");

        Assert.True(status.IsOnline);
        Assert.Equal("Online", status.Status);
    }

    [Fact]
    public async Task CheckStatusAsync_WhenOffline_ShouldReturnOfflineStatus()
    {
        _transportMock.Setup(t => t.CheckAvailabilityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var status = await _manager.CheckStatusAsync("tcp://192.168.1.50:9100");

        Assert.False(status.IsOnline);
        Assert.Contains("Offline", status.Status);
    }

    [Fact]
    public async Task GetLocalDevicesAsync_ShouldReturnValidResult()
    {
        var devices = await _manager.GetLocalDevicesAsync();

        Assert.NotNull(devices);
        Assert.NotNull(devices.SerialPorts);
        Assert.NotNull(devices.InstalledPrinters);
        Assert.False(string.IsNullOrEmpty(devices.MachineName));
    }
}
