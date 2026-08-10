using PDV.Domain.Enums;
using PDV.HardwareAgent.Transports;

namespace PDV.Tests.HardwareAgent;

public class TransportFactoryTests
{
    private readonly TransportFactory _factory = new();

    [Theory]
    [InlineData("192.168.1.50", "tcp://192.168.1.50:9100")]
    [InlineData("192.168.1.50:9100", "tcp://192.168.1.50:9100")]
    [InlineData("tcp://10.0.0.25:9200", "tcp://10.0.0.25:9200")]
    [InlineData("net://192.168.0.100:9100", "tcp://192.168.0.100:9100")]
    public void CreateTransport_TcpEndpoints_ShouldReturnTcpTransportWithNormalizedTarget(string input, string expectedEndpoint)
    {
        var transport = _factory.CreateTransport(input);

        Assert.NotNull(transport);
        Assert.Equal(PrinterConnectionType.Network, transport.ConnectionType);
        Assert.Equal(expectedEndpoint, transport.TargetEndpoint);
    }

    [Theory]
    [InlineData("usb://EPSON_TM_T20", "usb://EPSON_TM_T20")]
    [InlineData("usb://Generic_Text_Only", "usb://Generic_Text_Only")]
    public void CreateTransport_UsbEndpoints_ShouldReturnUsbTransport(string input, string expectedEndpoint)
    {
        var transport = _factory.CreateTransport(input);

        Assert.NotNull(transport);
        Assert.Equal(PrinterConnectionType.Usb, transport.ConnectionType);
        Assert.Equal(expectedEndpoint, transport.TargetEndpoint);
    }

    [Theory]
    [InlineData("serial://COM3?baud=9600", "serial://COM3?baud=9600")]
    [InlineData("com://COM1?baud=115200", "serial://COM1?baud=115200")]
    public void CreateTransport_SerialEndpoints_ShouldReturnSerialTransport(string input, string expectedEndpoint)
    {
        var transport = _factory.CreateTransport(input);

        Assert.NotNull(transport);
        Assert.Equal(PrinterConnectionType.Serial, transport.ConnectionType);
        Assert.Equal(expectedEndpoint, transport.TargetEndpoint);
    }

    [Fact]
    public void CreateTransport_EmptyTarget_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _factory.CreateTransport("   "));
    }

    [Fact]
    public void CreateTransport_UnsupportedScheme_ShouldThrowNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => _factory.CreateTransport("ftp://printer.local"));
    }
}
