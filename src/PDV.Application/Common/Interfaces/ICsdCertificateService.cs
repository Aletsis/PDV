using System;

namespace PDV.Application.Common.Interfaces;

public interface ICsdCertificateService
{
    CsdMetadata ExtractMetadata(byte[] cerBytes);
    string SignCadenaOriginal(string cadenaOriginal, byte[] keyBytes, string password);
}

public record CsdMetadata(string SerialNumber, DateTime ExpiresAt, string RfcEmisor, string CompanyName);
