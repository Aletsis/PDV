using PDV.Application.Common.Interfaces;

namespace PDV.Infrastructure.Common;

/// <summary>
/// Implementación del servicio de fecha/hora
/// </summary>
public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}
