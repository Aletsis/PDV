using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IGeocodingService
{
    /// <summary>Convierte una dirección física en coordenadas de latitud y longitud.</summary>
    Task<(double? Latitude, double? Longitude)> GeocodeAddressAsync(
        string street, 
        string city, 
        string state, 
        string zipCode, 
        string country, 
        CancellationToken cancellationToken = default);

    /// <summary>Convierte una cadena de texto de dirección completa en coordenadas.</summary>
    Task<(double? Latitude, double? Longitude)> GeocodeAddressQueryAsync(
        string addressQuery, 
        CancellationToken cancellationToken = default);

    /// <summary>Verifica si una coordenada geográfica está dentro de una zona de polígono.</summary>
    bool IsPointInPolygon(double latitude, double longitude, List<(double Lat, double Lng)> polygon);
}
