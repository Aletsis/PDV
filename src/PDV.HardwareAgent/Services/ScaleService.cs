using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PDV.Application.Common.Interfaces;

namespace PDV.HardwareAgent.Services;

public class ScaleService : IScaleService
{
    private static readonly SemaphoreSlim SerialLock = new(1, 1);

    public async Task<ScaleWeightDto> ReadWeightAsync(string portName, int baudRate, string protocol, CancellationToken cancellationToken = default)
    {
        if (string.Equals(portName, "MOCK", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(protocol, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            // Simulate reading delay
            await Task.Delay(150, cancellationToken);
            var randomWeight = Math.Round(0.5m + (decimal)Random.Shared.NextDouble() * 3.0m, 3);
            return new ScaleWeightDto(randomWeight, "kg", true, true, null);
        }

        await SerialLock.WaitAsync(cancellationToken);
        SerialPort? serialPort = null;
        try
        {
            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1500,
                WriteTimeout = 1500
            };

            serialPort.Open();

            if (string.Equals(protocol, "Torrey", StringComparison.OrdinalIgnoreCase))
            {
                // Send 'P' character (ASCII 0x50) to request weight
                serialPort.Write(new byte[] { 0x50 }, 0, 1);

                var responseBytes = new List<byte>();
                var start = DateTime.UtcNow;
                while ((DateTime.UtcNow - start).TotalMilliseconds < 1500)
                {
                    if (serialPort.BytesToRead > 0)
                    {
                        byte b = (byte)serialPort.ReadByte();
                        if (b == 0x0D || b == 0x0A) // CR or LF
                        {
                            if (responseBytes.Count > 0) break;
                        }
                        else
                        {
                            responseBytes.Add(b);
                        }
                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }

                string response = Encoding.ASCII.GetString(responseBytes.ToArray()).Trim();
                if (string.IsNullOrEmpty(response))
                {
                    return new ScaleWeightDto(0, "kg", false, false, "No data received from Torrey scale.");
                }

                decimal weightVal = ParseWeightString(response);
                return new ScaleWeightDto(weightVal, "kg", true, true, null);
            }
            else if (string.Equals(protocol, "Toledo", StringComparison.OrdinalIgnoreCase))
            {
                // Toledo typically outputs continuously: STX (0x02) + weight + tare + CR (0x0D)
                serialPort.DiscardInBuffer();
                var start = DateTime.UtcNow;

                while ((DateTime.UtcNow - start).TotalMilliseconds < 1500)
                {
                    if (serialPort.BytesToRead > 0)
                    {
                        int b = serialPort.ReadByte();
                        if (b == 0x02) // STX
                        {
                            var packetBytes = new List<byte>();
                            var packetStart = DateTime.UtcNow;
                            while ((DateTime.UtcNow - packetStart).TotalMilliseconds < 1000)
                            {
                                if (serialPort.BytesToRead > 0)
                                {
                                    int pb = serialPort.ReadByte();
                                    if (pb == 0x0D) break; // CR
                                    packetBytes.Add((byte)pb);
                                }
                                else
                                {
                                    await Task.Delay(5, cancellationToken);
                                }
                            }

                            string packetStr = Encoding.ASCII.GetString(packetBytes.ToArray());
                            var sb = new StringBuilder();
                            bool foundDigit = false;
                            foreach (char c in packetStr)
                            {
                                if (char.IsDigit(c) || c == '.' || c == '-')
                                {
                                    sb.Append(c);
                                    foundDigit = true;
                                }
                                else if (foundDigit)
                                {
                                    break;
                                }
                            }

                            string weightStr = sb.ToString();
                            if (!string.IsNullOrEmpty(weightStr))
                            {
                                decimal weightVal;
                                if (weightStr.Contains("."))
                                {
                                    weightVal = decimal.Parse(weightStr, System.Globalization.CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    weightVal = decimal.Parse(weightStr, System.Globalization.CultureInfo.InvariantCulture) / 1000m;
                                }
                                return new ScaleWeightDto(weightVal, "kg", true, true, null);
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }

                return new ScaleWeightDto(0, "kg", false, false, "Timeout waiting for Toledo STX/CR packet.");
            }
            else
            {
                return new ScaleWeightDto(0, "kg", false, false, $"Protocol '{protocol}' not supported.");
            }
        }
        catch (Exception ex)
        {
            return new ScaleWeightDto(0, "kg", false, false, $"Serial error: {ex.Message}");
        }
        finally
        {
            if (serialPort != null)
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
                serialPort.Dispose();
            }
            SerialLock.Release();
        }
    }

    private decimal ParseWeightString(string input)
    {
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsDigit(c) || c == '.' || c == '-')
            {
                sb.Append(c);
            }
        }
        if (sb.Length == 0) return 0;
        return decimal.Parse(sb.ToString(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
