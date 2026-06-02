using PDV.Domain.Entities;

namespace PDV.Application.Features.Cashiers.Dtos;

public class CashierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? UserId { get; set; }
}
