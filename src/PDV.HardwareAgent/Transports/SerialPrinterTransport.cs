using System.IO.Ports;
using PDV.Domain.Enums;

namespace PDV.HardwareAgent.Transports;

public class SerialPrinterTransport : IPrinterTransport
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private readonly Handshake _handshake;
    private readonly int _timeoutMs;

    private static readonly SemaphoreSlim _portLock = new(1, 1);

    public PrinterConnectionType ConnectionType => PrinterConnectionType.Serial;
    public string TargetEndpoint => $"serial://{_portName}?baud={_baudRate}";

    public SerialPrinterTransport(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        Handshake handshake = Handshake.None,
        int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("El nombre del puerto serial (COMx / tty) es requerido.", nameof(portName));

        _portName = portName.Trim();
        _baudRate = baudRate <= 0 ? 9600 : baudRate;
        _parity = parity;
        _dataBits = dataBits <= 0 ? 8 : dataBits;
        _stopBits = stopBits;
        _handshake = handshake;
        _timeoutMs = timeoutMs <= 0 ? 3000 : timeoutMs;
    }

    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        await _portLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var availablePorts = SerialPort.GetPortNames();
            if (!availablePorts.Any(p => p.Equals(_portName, StringComparison.OrdinalIgnoreCase)))
            {
                // Port name might not be strictly listed if on virtual driver, attempt test open
                try
                {
                    using var testPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
                    {
                        ReadTimeout = _timeoutMs,
                        WriteTimeout = _timeoutMs
                    };
                    testPort.Open();
                    var opened = testPort.IsOpen;
                    testPort.Close();
                    return opened;
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _portLock.Release();
        }
    }

    public async Task SendBytesAsync(byte[] data, CancellationToken ct = default)
    {
        await _portLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using var serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
                {
                    Handshake = _handshake,
                    ReadTimeout = _timeoutMs,
                    WriteTimeout = _timeoutMs
                };

                serialPort.Open();
                serialPort.Write(data, 0, data.Length);

                // Dar tiempo al buffer físico para drenar
                Thread.Sleep(200);
                serialPort.Close();
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _portLock.Release();
        }
    }

    public void Dispose() { }
}
