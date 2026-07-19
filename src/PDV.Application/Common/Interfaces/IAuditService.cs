namespace PDV.Application.Common.Interfaces;

public interface IAuditService
{
    string? CurrentAction { get; set; }
}
