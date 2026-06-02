using System;

namespace PDV.Application.Features.UnidadesMedida;

public class UnidadMedidaDto
{
    public Guid Id { get; set; }
    public int ExternalId { get; set; }
    public string NombreUnidad { get; set; } = string.Empty;
    public string Abreviatura { get; set; } = string.Empty;
    public string Despliegue { get; set; } = string.Empty;
    public string ClaveInt { get; set; } = string.Empty;
    public string ClaveSat { get; set; } = string.Empty;
}
