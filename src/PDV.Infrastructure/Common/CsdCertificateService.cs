using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using PDV.Application.Common.Interfaces;

namespace PDV.Infrastructure.Common;

public class CsdCertificateService : ICsdCertificateService
{
    public CsdMetadata ExtractMetadata(byte[] cerBytes)
    {
        if (cerBytes == null || cerBytes.Length == 0)
        {
            throw new ArgumentException("Certificate bytes cannot be empty.", nameof(cerBytes));
        }

        using var certificate = new X509Certificate2(cerBytes);
        
        string serialNumber = CleanSerialNumber(certificate.SerialNumber);
        DateTime expiresAt = certificate.NotAfter;
        string subject = certificate.Subject;
        
        string rfc = ExtractRfcFromSubject(subject);
        string companyName = certificate.GetNameInfo(X509NameType.SimpleName, false) ?? "";

        return new CsdMetadata(serialNumber, expiresAt, rfc, companyName);
    }

    public string SignCadenaOriginal(string cadenaOriginal, byte[] keyBytes, string password)
    {
        if (string.IsNullOrEmpty(cadenaOriginal))
        {
            throw new ArgumentException("Cadena original cannot be empty.", nameof(cadenaOriginal));
        }
        if (keyBytes == null || keyBytes.Length == 0)
        {
            throw new ArgumentException("Key bytes cannot be empty.", nameof(keyBytes));
        }
        if (password == null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        using var rsa = RSA.Create();
        rsa.ImportEncryptedPkcs8PrivateKey(password.AsSpan(), keyBytes.AsSpan(), out _);

        byte[] dataToSign = Encoding.UTF8.GetBytes(cadenaOriginal);
        byte[] signatureBytes = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signatureBytes);
    }

    private string CleanSerialNumber(string rawHex)
    {
        if (string.IsNullOrEmpty(rawHex))
            return "";

        try
        {
            // Convert hex representation to ASCII (SAT serial numbers are stored as hex-encoded ASCII digits)
            var cleanHex = rawHex.Replace(" ", "").Replace("-", "");
            
            // If length is odd, prefix with '0'
            if (cleanHex.Length % 2 != 0)
            {
                cleanHex = "0" + cleanHex;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < cleanHex.Length; i += 2)
            {
                string hs = cleanHex.Substring(i, 2);
                if (byte.TryParse(hs, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                {
                    sb.Append((char)b);
                }
            }

            var ascii = sb.ToString();
            // SAT serial numbers are exactly 20 digits long
            if (ascii.Length == 20 && Regex.IsMatch(ascii, "^[0-9]+$"))
            {
                return ascii;
            }
        }
        catch
        {
            // Fallback to raw hex if anything fails
        }

        return rawHex;
    }

    private string ExtractRfcFromSubject(string subject)
    {
        if (string.IsNullOrEmpty(subject))
            return "";

        // Common OID for RFC in Mexican digital certificates is OID.2.5.4.45 (x500UniqueIdentifier)
        // or it might contain "RFC="
        var rfcMatch = Regex.Match(subject, @"(?:2\.5\.4\.45|x500UniqueIdentifier|RFC)\s*=\s*([A-Z&Ñ]{3,4}[0-9]{6}[A-Z0-9]{3})", RegexOptions.IgnoreCase);
        if (rfcMatch.Success)
        {
            return rfcMatch.Groups[1].Value.ToUpper();
        }

        // Fallback: look for any 12 or 13 character string matching Mexican RFC format
        var generalRfcMatch = Regex.Match(subject, @"\b([A-Z&Ñ]{3,4}[0-9]{6}[A-Z0-9]{3})\b", RegexOptions.IgnoreCase);
        if (generalRfcMatch.Success)
        {
            return generalRfcMatch.Groups[1].Value.ToUpper();
        }

        return "";
    }
}
