using System;

namespace PDV.Application.Features.Departments.Dtos;

public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ClasificacionId { get; set; }
    public bool IsActive { get; set; }
}
