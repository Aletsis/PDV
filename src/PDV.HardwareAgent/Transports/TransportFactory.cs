using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Web;
using PDV.Domain.Enums;

namespace PDV.HardwareAgent.Transports;

public interface ITransportFactory
{
    IPrinterTransport CreateTransport(string targetEndpoint, int timeoutMs = 5000);
}

public class TransportFactory : ITransportFactory
{
    public IPrinterTransport CreateTransport(string targetEndpoint, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(targetEndpoint))
            throw new ArgumentException("El destino del endpoint de la impresora no puede estar vacío.", nameof(targetEndpoint));

        var clean = targetEndpoint.Trim();

        // Si no tiene esquema, asumir TCP (ej. "192.168.1.50" o "192.168.1.50:9100")
        if (!clean.Contains("://"))
        {
            var parts = clean.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 9100;
            return new TcpPrinterTransport(host, port, timeoutMs);
        }

        var schemeEnd = clean.IndexOf("://", StringComparison.Ordinal);
        var scheme = clean.Substring(0, schemeEnd).ToLowerInvariant();
        var remainder = clean.Substring(schemeEnd + 3);

        // Separar ruta/host de query string (?...)
        var queryIndex = remainder.IndexOf('?');
        var pathPart = queryIndex >= 0 ? remainder.Substring(0, queryIndex) : remainder;
        var queryPart = queryIndex >= 0 ? remainder.Substring(queryIndex) : string.Empty;

        // Limpiar leading slashes (ej: usb:///dev/usb/lp0 -> /dev/usb/lp0 vs usb://POS-80 -> POS-80)
        string deviceOrHost;
        if (pathPart.StartsWith("//"))
        {
            deviceOrHost = "/" + pathPart.TrimStart('/');
        }
        else if (pathPart.StartsWith("/"))
        {
            deviceOrHost = pathPart.TrimStart('/');
        }
        else
        {
            deviceOrHost = pathPart;
        }

        switch (scheme)
        {
            case "tcp":
            case "net":
            {
                var uri = new Uri(clean);
                var host = uri.Host;
                var port = uri.Port <= 0 ? 9100 : uri.Port;
                return new TcpPrinterTransport(host, port, timeoutMs);
            }

            case "usb":
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return new WindowsUsbPrinterTransport(deviceOrHost);
                }
                else
                {
                    var devPath = deviceOrHost.StartsWith("/") ? deviceOrHost : $"/dev/usb/{deviceOrHost}";
                    return new PosixUsbPrinterTransport(devPath);
                }
            }

            case "serial":
            case "com":
            {
                var portName = deviceOrHost;
                var baudRate = 9600;
                var parity = Parity.None;
                var dataBits = 8;
                var stopBits = StopBits.One;
                var handshake = Handshake.None;

                if (!string.IsNullOrEmpty(queryPart))
                {
                    var query = HttpUtility.ParseQueryString(queryPart);
                    if (int.TryParse(query["baud"] ?? query["baudrate"], out var parsedBaud))
                    {
                        baudRate = parsedBaud;
                    }

                    if (Enum.TryParse<Parity>(query["parity"], true, out var parsedParity))
                    {
                        parity = parsedParity;
                    }

                    if (int.TryParse(query["databits"] ?? query["data"], out var parsedDataBits))
                    {
                        dataBits = parsedDataBits;
                    }

                    if (Enum.TryParse<StopBits>(query["stopbits"] ?? query["stop"], true, out var parsedStopBits))
                    {
                        stopBits = parsedStopBits;
                    }

                    if (Enum.TryParse<Handshake>(query["handshake"] ?? query["flow"], true, out var parsedHandshake))
                    {
                        handshake = parsedHandshake;
                    }
                }

                return new SerialPrinterTransport(portName, baudRate, parity, dataBits, stopBits, handshake, timeoutMs);
            }

            default:
                throw new NotSupportedException($"Esquema de conexión '{scheme}' no soportado por el HardwareAgent.");
        }
    }
}
