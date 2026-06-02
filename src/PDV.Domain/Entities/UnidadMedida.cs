using PDV.Domain.Common;

namespace PDV.Domain.Entities;

public class UnidadMedida : BaseEntity, IAggregateRoot
{
    public int ExternalId { get; private set; }
    public string NombreUnidad { get; private set; } = string.Empty;
    public string Abreviatura { get; private set; } = string.Empty;
    public string Despliegue { get; private set; } = string.Empty;
    public string ClaveInt { get; private set; } = string.Empty;
    public string ClaveSat { get; private set; } = string.Empty;

    private UnidadMedida() { } // Para EF Core

    public UnidadMedida(int externalId, string nombreUnidad, string abreviatura, string despliegue, string claveInt, string claveSat)
    {
        ExternalId = externalId;
        NombreUnidad = nombreUnidad?.Trim() ?? string.Empty;
        Abreviatura = abreviatura?.Trim() ?? string.Empty;
        Despliegue = despliegue?.Trim() ?? string.Empty;
        ClaveInt = claveInt?.Trim() ?? string.Empty;
        ClaveSat = claveSat?.Trim() ?? string.Empty;
    }

    public void Update(string nombreUnidad, string abreviatura, string despliegue, string claveInt, string claveSat)
    {
        NombreUnidad = nombreUnidad?.Trim() ?? string.Empty;
        Abreviatura = abreviatura?.Trim() ?? string.Empty;
        Despliegue = despliegue?.Trim() ?? string.Empty;
        ClaveInt = claveInt?.Trim() ?? string.Empty;
        ClaveSat = claveSat?.Trim() ?? string.Empty;
    }
}
