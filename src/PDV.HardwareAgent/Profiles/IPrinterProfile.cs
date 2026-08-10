using PDV.HardwareAgent.Contracts.Enums;

namespace PDV.HardwareAgent.Profiles;

public interface IPrinterProfile
{
    PrinterProfileType ProfileType { get; }

    byte[] Initialize();

    byte[] CutPaper(bool partialCut = true);

    byte[] OpenCashDrawer(int pin = 0);

    byte[] FormatText(string text, int codePage = 1252);

    byte[] PrintBarcode(string data, int barcodeType = 73, int height = 100);

    byte[] PrintQr(string data, int moduleSize = 4, int errorLevel = 48);

    byte[] ConvertRasterImage(byte[] imagePngBytes, int maxWidth = 384);
}
