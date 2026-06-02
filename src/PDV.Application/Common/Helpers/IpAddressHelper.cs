using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PDV.Application.Common.Helpers;

public static class IpAddressHelper
{
    private static readonly string[] LoopbackAddresses = { "::1", "127.0.0.1", "0.0.0.1", "localhost" };

    /// <summary>
    /// Obtiene todas las direcciones IPv4 locales activas de las interfaces de red físicas del equipo.
    /// </summary>
    public static List<string> GetLocalIpAddresses()
    {
        var ipList = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Solo considerar interfaces activas, que no sean loopback ni virtuales/pseudointerfaces
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    // Descartar interfaces de túneles o VPNs comunes si es posible
                    string name = ni.Name.ToLower();
                    string desc = ni.Description.ToLower();
                    if (name.Contains("loopback") || name.Contains("virtual") || 
                        desc.Contains("virtual") || desc.Contains("pseudo") || 
                        name.Contains("vmware") || name.Contains("virtualbox") || 
                        name.Contains("docker") || name.Contains("vpn"))
                    {
                        continue;
                    }

                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork) // Solo IPv4
                        {
                            ipList.Add(addr.Address.ToString());
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback en caso de error accediendo a NetworkInterface
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipList.Add(ip.ToString());
                    }
                }
            }
            catch
            {
                // Ignorar
            }
        }

        // Si no se encontró ninguna IP, agregar fallback de localhost
        var result = ipList.Distinct().ToList();
        return result;
    }

    /// <summary>
    /// Resuelve la dirección IP del cliente. Si detecta una dirección de loopback (ej: ::1 o 0.0.0.1),
    /// retorna la primera dirección IPv4 física activa del equipo.
    /// </summary>
    public static string ResolveClientIp(string? rawIp)
    {
        var normalized = rawIp?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return "127.0.0.1";

        if (IsLoopback(normalized))
        {
            var localIps = GetLocalIpAddresses();
            if (localIps.Any())
            {
                return localIps.First(); // Ej: 192.168.0.60
            }
            return "127.0.0.1";
        }

        return normalized;
    }

    /// <summary>
    /// Valida si una dirección IP corresponde a loopback o local.
    /// </summary>
    public static bool IsLoopback(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return true;
        return LoopbackAddresses.Contains(ip) || ip.StartsWith("fe80", StringComparison.OrdinalIgnoreCase);
    }
}
