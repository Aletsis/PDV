namespace PDV.Application.Common.Interfaces;

/// <summary>
/// Servicio para obtener la fecha/hora actual
/// Permite controlar el tiempo en tests y facilitar migración entre plataformas
/// </summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}
