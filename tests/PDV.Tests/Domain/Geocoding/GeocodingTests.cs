using System.Collections.Generic;
using PDV.Infrastructure.Common;
using Xunit;

namespace PDV.Tests.Domain.Geocoding;

public class GeocodingTests
{
    [Fact]
    public void IsPointInPolygon_WithPointInside_ReturnsTrue()
    {
        // Arrange
        // Un cuadrado simple de 1x1 grado
        var polygon = new List<(double Lat, double Lng)>
        {
            (20.0, -104.0),
            (21.0, -104.0),
            (21.0, -103.0),
            (20.0, -103.0)
        };

        var geocodingService = new GeocodingService(new System.Net.Http.HttpClient());

        // Act & Assert
        // Punto en el centro exacto
        Assert.True(geocodingService.IsPointInPolygon(20.5, -103.5, polygon));
    }

    [Fact]
    public void IsPointInPolygon_WithPointOutside_ReturnsFalse()
    {
        // Arrange
        var polygon = new List<(double Lat, double Lng)>
        {
            (20.0, -104.0),
            (21.0, -104.0),
            (21.0, -103.0),
            (20.0, -103.0)
        };

        var geocodingService = new GeocodingService(new System.Net.Http.HttpClient());

        // Act & Assert
        // Punto claramente fuera
        Assert.False(geocodingService.IsPointInPolygon(22.0, -102.0, polygon));
    }
}
