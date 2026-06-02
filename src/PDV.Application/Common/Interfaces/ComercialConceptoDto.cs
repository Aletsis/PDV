using System.Text.Json.Serialization;

namespace PDV.Application.Common.Interfaces;

public class ComercialConceptoDto
{
    [JsonPropertyName("cidconceptodocumento")]
    public int Id { get; set; }

    [JsonPropertyName("ccodigoconcepto")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("cnombreconcepto")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("cserieporomision")]
    public string? Serie { get; set; }

    [JsonPropertyName("cnofolio")]
    public double Folio { get; set; }
}
