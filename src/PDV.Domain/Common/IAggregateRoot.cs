namespace PDV.Domain.Common;

/// <summary>
/// Marca una entidad como Raíz de Agregado.
/// Solo los agregados raíz deben tener repositorios propios.
/// Garantiza que el acceso a las entidades hijas pase siempre por su raíz.
/// </summary>
public interface IAggregateRoot
{
}
