using PDV.Application.Common.Interfaces;

namespace PDV.Application.Common.Services;

public class AuditService : IAuditService
{
    public string? CurrentAction { get; set; }
}
