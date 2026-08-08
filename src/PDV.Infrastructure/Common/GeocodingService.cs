using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PDV.Application.Common.Interfaces;

namespace PDV.Infrastructure.Common;

public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;

    public GeocodingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        // Nominatim requiere un User-Agent identificable
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PDV-App-Geocoding-Service/1.0");
        }
    }

    public async Task<(double? Latitude, double? Longitude)> GeocodeAddressQueryAsync(
        string addressQuery, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(addressQuery)) return (null, null);

        try
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(addressQuery)}&format=json&limit=1";

            var response = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(url, cancellationToken);
            if (response != null && response.Count > 0)
            {
                var first = response[0];
                if (double.TryParse(first.Lat, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(first.Lon, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                {
                    return (lat, lon);
                }
            }
        }
        catch
        {
            // En caso de error o estar offline, devolver nulo silenciosamente.
        }

        return (null, null);
    }

    public async Task<(double? Latitude, double? Longitude)> GeocodeAddressAsync(
        string street, 
        string city, 
        string state, 
        string zipCode, 
        string country, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Formatear dirección para búsqueda simple
            string query = $"{street}, {city}, {state}, {zipCode}, {country}";
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1";

            var response = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(url, cancellationToken);
            if (response != null && response.Count > 0)
            {
                var first = response[0];
                if (double.TryParse(first.Lat, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(first.Lon, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                {
                    return (lat, lon);
                }
            }
        }
        catch
        {
            // En caso de error o estar offline, devolver nulo silenciosamente.
            // Se asume que el sistema continuará en modo manual sin interrumpir la operación.
        }

        return (null, null);
    }

    public bool IsPointInPolygon(double latitude, double longitude, List<(double Lat, double Lng)> polygon)
    {
        if (polygon == null || polygon.Count < 3) return false;

        bool inside = false;
        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            if (((polygon[i].Lat > latitude) != (polygon[j].Lat > latitude)) &&
                (longitude < (polygon[j].Lng - polygon[i].Lng) * (latitude - polygon[i].Lat) / (polygon[j].Lat - polygon[i].Lat) + polygon[i].Lng))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;
    }
}
