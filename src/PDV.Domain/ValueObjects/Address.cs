namespace PDV.Domain.ValueObjects;

public record Address
{
    public string Street { get; init; } = string.Empty;
    public string? ExteriorNumber { get; init; }
    public string? InteriorNumber { get; init; }
    public string? Colony { get; init; }
    public string ZipCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Country { get; init; } = "México";

    private Address() { } // For EF Core

    public Address(
        string street,
        string city,
        string state,
        string zipCode,
        string country = "México",
        string? exteriorNumber = null,
        string? interiorNumber = null,
        string? colony = null)
    {
        Street = street ?? string.Empty;
        City = city ?? string.Empty;
        State = state ?? string.Empty;
        ZipCode = zipCode ?? string.Empty;
        Country = string.IsNullOrWhiteSpace(country) ? "México" : country;
        ExteriorNumber = exteriorNumber;
        InteriorNumber = interiorNumber;
        Colony = colony;
    }

    public static Address Create(
        string street,
        string city,
        string state,
        string zipCode,
        string country = "México",
        string? exteriorNumber = null,
        string? interiorNumber = null,
        string? colony = null)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Street cannot be empty", nameof(street));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City cannot be empty", nameof(city));
        if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("State cannot be empty", nameof(state));
        if (string.IsNullOrWhiteSpace(country)) country = "México";

        return new Address(street, city, state, zipCode, country, exteriorNumber, interiorNumber, colony);
    }

    /// <summary>
    /// Genera la representación textual estructurada y completa de la dirección para visualización, tickets o geocodificación.
    /// </summary>
    public string ToFullAddressString()
    {
        var parts = new List<string>();

        var streetPart = Street?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(ExteriorNumber))
            streetPart += $" #{ExteriorNumber.Trim()}";
        if (!string.IsNullOrWhiteSpace(InteriorNumber))
            streetPart += $" Int. {InteriorNumber.Trim()}";

        if (!string.IsNullOrWhiteSpace(streetPart))
            parts.Add(streetPart);

        if (!string.IsNullOrWhiteSpace(Colony))
            parts.Add($"Col. {Colony.Trim()}");

        if (!string.IsNullOrWhiteSpace(ZipCode) && ZipCode != "00000")
            parts.Add($"C.P. {ZipCode.Trim()}");

        if (!string.IsNullOrWhiteSpace(City) && City != "N/A")
            parts.Add(City.Trim());

        if (!string.IsNullOrWhiteSpace(State) && State != "N/A")
            parts.Add(State.Trim());

        if (!string.IsNullOrWhiteSpace(Country))
            parts.Add(Country.Trim());

        return string.Join(", ", parts);
    }
}
