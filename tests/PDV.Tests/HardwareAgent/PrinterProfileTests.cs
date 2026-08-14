using PDV.HardwareAgent.Contracts.Enums;
using PDV.HardwareAgent.Profiles;

namespace PDV.Tests.HardwareAgent;

public class PrinterProfileTests
{
    private readonly PrinterProfileFactory _factory = new();

    [Fact]
    public void Factory_ShouldResolveCorrectProfile()
    {
        Assert.IsType<EscPosProfile>(_factory.GetProfile(PrinterProfileType.EscPos));
        Assert.IsType<StarPrntProfile>(_factory.GetProfile(PrinterProfileType.StarPrnt));
        Assert.IsType<GenericThermalProfile>(_factory.GetProfile(PrinterProfileType.GenericThermal));
        Assert.IsType<StarPrntProfile>(_factory.GetProfile("star"));
        Assert.IsType<GenericThermalProfile>(_factory.GetProfile("generic"));
        Assert.IsType<EscPosProfile>(_factory.GetProfile("epson"));
    }

    [Fact]
    public void EscPosProfile_Commands_ShouldMatchSpecification()
    {
        var profile = new EscPosProfile();

        // 1. Init: ESC @
        var init = profile.Initialize();
        Assert.Equal(new byte[] { 0x1B, 0x40 }, init);

        // 2. Cut: ESC d 7 + GS V 1 (partial)
        var cut = profile.CutPaper(partialCut: true);
        Assert.Equal(new byte[] { 0x1B, 0x64, 0x07, 0x1D, 0x56, 0x01 }, cut);

        // 3. Open Drawer pin 0: ESC p 0 25 250
        var drawer = profile.OpenCashDrawer(pin: 0);
        Assert.Equal(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, drawer);

        // 4. Text encoding: should produce non-empty byte array
        var textBytes = profile.FormatText("¡HOLA MUNDO!", 1252);
        Assert.NotEmpty(textBytes);
    }

    [Fact]
    public void StarPrntProfile_Commands_ShouldMatchSpecification()
    {
        var profile = new StarPrntProfile();

        // 1. Init: ESC ? LF NUL
        var init = profile.Initialize();
        Assert.Equal(new byte[] { 0x1B, 0x3F, 0x0A, 0x00 }, init);

        // 2. Cut partial: ESC d 2
        var cut = profile.CutPaper(partialCut: true);
        Assert.Equal(new byte[] { 0x1B, 0x64, 0x02 }, cut);

        // 3. Open Drawer pin 0: BEL (0x07)
        var drawer = profile.OpenCashDrawer(pin: 0);
        Assert.Equal(new byte[] { 0x07 }, drawer);
    }

    [Fact]
    public void GenericThermalProfile_Commands_ShouldUseGenericCut()
    {
        var profile = new GenericThermalProfile();

        var cut = profile.CutPaper(partialCut: true);
        Assert.Equal(new byte[] { 0x1B, 0x64, 0x07, 0x1B, 0x69 }, cut);
    }
}
