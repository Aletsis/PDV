using System;
using PDV.Domain.Enums;

namespace PDV.Application.Features.InventoryMovements.Dtos;

public class InventoryMovementDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public InventoryMovementType Type { get; set; }
    public DateTime Date { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Remarks { get; set; }
}
