using PDV.Domain.Enums;

namespace PDV.Application.Features.Shifts.Dtos;

public class ShiftDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string CashRegisterName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal InitialCash { get; set; }
    public DateTime StartTime { get; set; }
    public ShiftStatus Status { get; set; }
}
