using MediatR;
using PDV.Application.Common.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Behaviors;

public class AuditLogBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditService _auditService;

    public AuditLogBehavior(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Guardar el nombre de la acción/comando que se está procesando
        _auditService.CurrentAction = request.GetType().Name;

        try
        {
            return await next();
        }
        finally
        {
            // Limpiar al terminar
            _auditService.CurrentAction = null;
        }
    }
}
