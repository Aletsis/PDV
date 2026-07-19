using System;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Common.Interfaces;

public interface IPacService
{
    Task<PacStampResult> StampXmlAsync(string xml, string apiUser, string apiKey, string pacUrl, CancellationToken cancellationToken);
    
    Task<PacCancelResult> CancelInvoiceAsync(
        string uuid, 
        string rfcEmisor, 
        string rfcReceptor, 
        decimal total, 
        string motivo, 
        string? uuidSustituto, 
        string apiUser, 
        string apiKey, 
        string pacUrl, 
        CancellationToken cancellationToken);
}

public record PacStampResult(
    bool Success, 
    string? ErrorMessage, 
    string? StampedXml, 
    string? Uuid, 
    DateTime? StampedAt, 
    string? SelloSAT, 
    string? CertificadoSAT, 
    string? CadenaOriginalTfd);

public record PacCancelResult(
    bool Success, 
    string? ErrorMessage, 
    string? AcuseXml, 
    DateTime? CancelledAt);
