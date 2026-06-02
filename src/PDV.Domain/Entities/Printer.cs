using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using System.Net;

namespace PDV.Domain.Entities;

public class Printer : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public PrinterConnectionType ConnectionType { get; private set; }
    public bool IsActive { get; private set; }

    // ──────────────────────────────────────────────
    // Parámetros de red (Network / Bluetooth)
    // ──────────────────────────────────────────────
    public string? IpAddress { get; private set; }
    public int? Port { get; private set; }

    // ──────────────────────────────────────────────
    // Parámetros de dispositivo local (USB / Serial)
    // ──────────────────────────────────────────────
    /// <summary>Ruta del dispositivo. Ej: "USB001", "COM3", "LPT1"</summary>
    public string? DevicePath { get; private set; }

    // ──────────────────────────────────────────────
    // Configuración de impresión ESC/POS
    // ──────────────────────────────────────────────
    /// <summary>Página de código para la codificación de caracteres. Ej: 1252 (Windows Latin-1).</summary>
    public int CodePage { get; private set; }
    /// <summary>Ancho máximo de impresión en dots. 384 = papel 80mm, 576 = papel 80mm alta resolución.</summary>
    public int MaxWidth { get; private set; }

    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }

#pragma warning disable CS8618
    private Printer() { } // Para EF Core
#pragma warning restore CS8618

    public Printer(
        string name,
        PrinterConnectionType connectionType,
        int codePage = 1252,
        int maxWidth = 384,
        string? ipAddress = null,
        int? port = null,
        string? devicePath = null,
        Guid? branchId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la impresora es requerido.");
        if (codePage <= 0)
            throw new DomainException("La página de código debe ser un valor positivo.");
        if (maxWidth <= 0)
            throw new DomainException("El ancho máximo de impresión debe ser mayor a cero.");

        ValidateConnectionParams(connectionType, ipAddress, port, devicePath);

        Name = name.Trim();
        ConnectionType = connectionType;
        CodePage = codePage;
        MaxWidth = maxWidth;
        IpAddress = ipAddress?.Trim();
        Port = port;
        DevicePath = devicePath?.Trim();
        BranchId = branchId;
        IsActive = true;

        AddDomainEvent(new PrinterCreatedEvent(Id, Name, ConnectionType));
    }

    public void Update(
        string name,
        int codePage,
        int maxWidth,
        string? ipAddress = null,
        int? port = null,
        string? devicePath = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la impresora es requerido.");
        if (codePage <= 0)
            throw new DomainException("La página de código debe ser un valor positivo.");
        if (maxWidth <= 0)
            throw new DomainException("El ancho máximo de impresión debe ser mayor a cero.");

        ValidateConnectionParams(ConnectionType, ipAddress, port, devicePath);

        Name = name.Trim();
        CodePage = codePage;
        MaxWidth = maxWidth;
        IpAddress = ipAddress?.Trim();
        Port = port;
        DevicePath = devicePath?.Trim();

        AddDomainEvent(new PrinterUpdatedEvent(Id, Name));
    }

    public void Activate()
    {
        if (IsActive) throw new DomainException("La impresora ya está activa.");
        IsActive = true;
        AddDomainEvent(new PrinterActivatedEvent(Id));
    }

    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("La impresora ya está inactiva.");
        IsActive = false;
        AddDomainEvent(new PrinterDeactivatedEvent(Id));
    }

    private static void ValidateConnectionParams(
        PrinterConnectionType type,
        string? ipAddress,
        int? port,
        string? devicePath)
    {
        if (type == PrinterConnectionType.Network || type == PrinterConnectionType.Bluetooth)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new DomainException($"La dirección IP es requerida para impresoras de tipo {type}.");
            if (!IPAddress.TryParse(ipAddress.Trim(), out _))
                throw new DomainException($"La dirección IP '{ipAddress}' no tiene un formato válido.");
            if (!port.HasValue || port <= 0 || port > 65535)
                throw new DomainException("El puerto debe ser un valor entre 1 y 65535.");
        }

        if (type == PrinterConnectionType.Usb || type == PrinterConnectionType.Serial)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
                throw new DomainException($"La ruta del dispositivo es requerida para impresoras de tipo {type}. Ej: 'USB001', 'COM3'.");
        }
    }
}
