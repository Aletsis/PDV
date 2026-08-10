using System.Net.Sockets;
using PDV.Domain.Enums;

namespace PDV.HardwareAgent.Transports;

public class TcpPrinterTransport : IPrinterTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;

    public PrinterConnectionType ConnectionType => PrinterConnectionType.Network;
    public string TargetEndpoint => $"tcp://{_host}:{_port}";

    public TcpPrinterTransport(string host, int port = 9100, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("El host o dirección IP es requerido.", nameof(host));

        _host = host.Trim();
        _port = port <= 0 ? 9100 : port;
        _timeoutMs = timeoutMs <= 0 ? 5000 : timeoutMs;
    }

    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(_timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await client.ConnectAsync(_host, _port, linked.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public async Task SendBytesAsync(byte[] data, CancellationToken ct = default)
    {
        using var client = new TcpClient
        {
            SendTimeout = _timeoutMs,
            ReceiveTimeout = _timeoutMs,
            LingerState = new LingerOption(true, 2)
        };

        using var timeoutCts = new CancellationTokenSource(_timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        await client.ConnectAsync(_host, _port, linked.Token).ConfigureAwait(false);
        using var stream = client.GetStream();
        await stream.WriteAsync(data, 0, data.Length, linked.Token).ConfigureAwait(false);
        await stream.FlushAsync(linked.Token).ConfigureAwait(false);

        // Pequeña pausa para asegurar transmisión de buffer antes de cerrar socket
        await Task.Delay(100, linked.Token).ConfigureAwait(false);
    }

    public void Dispose() { }
}
