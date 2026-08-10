using PDV.HardwareAgent.Contracts.Enums;

namespace PDV.HardwareAgent.Profiles;

public class GenericThermalProfile : EscPosProfile
{
    public new PrinterProfileType ProfileType => PrinterProfileType.GenericThermal;

    public override byte[] CutPaper(bool partialCut = true)
    {
        // Many generic 58mm/80mm Chinese printers use GS V 66 0 or ESC i
        return new byte[]
        {
            0x1B, 0x64, 0x02, // Feed 2 lines
            0x1B, 0x69        // ESC i (Generic partial cut)
        };
    }
}
