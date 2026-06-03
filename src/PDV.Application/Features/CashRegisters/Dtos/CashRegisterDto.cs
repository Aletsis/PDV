using PDV.Domain.Enums;

namespace PDV.Application.Features.CashRegisters.Dtos;

public class CashRegisterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal InitialCash { get; set; }

    /// <summary>Dirección IP vinculada a esta caja. Null si no tiene IP asignada.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Modo de operación de la caja (Piso de ventas / Pedidos).</summary>
    public CashRegisterMode Mode { get; set; }

    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    public string? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }

    public Guid? AssignedPrinterId { get; set; }
    public string? AssignedPrinterName { get; set; }
}

