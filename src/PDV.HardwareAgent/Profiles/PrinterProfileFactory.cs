using PDV.HardwareAgent.Contracts.Enums;

namespace PDV.HardwareAgent.Profiles;

public interface IPrinterProfileFactory
{
    IPrinterProfile GetProfile(PrinterProfileType profileType);
    IPrinterProfile GetProfile(string? profileName);
}

public class PrinterProfileFactory : IPrinterProfileFactory
{
    public IPrinterProfile GetProfile(PrinterProfileType profileType)
    {
        return profileType switch
        {
            PrinterProfileType.StarPrnt => new StarPrntProfile(),
            PrinterProfileType.GenericThermal => new GenericThermalProfile(),
            _ => new EscPosProfile()
        };
    }

    public IPrinterProfile GetProfile(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return new EscPosProfile();

        if (Enum.TryParse<PrinterProfileType>(profileName, true, out var parsed))
            return GetProfile(parsed);

        var normalized = profileName.Trim().ToLowerInvariant();
        if (normalized.Contains("star")) return new StarPrntProfile();
        if (normalized.Contains("generic") || normalized.Contains("generica")) return new GenericThermalProfile();

        return new EscPosProfile();
    }
}
