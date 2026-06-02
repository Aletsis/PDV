using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class Logo : BaseEntity, IAggregateRoot
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/svg+xml"
    };

    // Límite de 2 MB para logos
    private const int MaxFileSizeBytes = 2 * 1024 * 1024;

    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public byte[] Data { get; private set; }
    public int FileSizeBytes { get; private set; }

    public LogoPurpose Purpose { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Sucursal a la que pertenece el logo. Nulo = logo global de empresa.</summary>
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }

#pragma warning disable CS8618
    private Logo() { } // Para EF Core
#pragma warning restore CS8618

    public Logo(string fileName, string contentType, byte[] data, LogoPurpose purpose, Guid? branchId = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("El nombre de archivo del logo es requerido.");

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
            throw new DomainException($"Tipo de contenido inválido. Permitidos: {string.Join(", ", AllowedContentTypes)}.");

        if (data is null || data.Length == 0)
            throw new DomainException("Los datos del logo no pueden estar vacíos.");

        if (data.Length > MaxFileSizeBytes)
            throw new DomainException($"El logo supera el tamaño máximo permitido de {MaxFileSizeBytes / 1024 / 1024} MB.");

        FileName = fileName.Trim();
        ContentType = contentType.Trim().ToLowerInvariant();
        Data = data;
        FileSizeBytes = data.Length;
        Purpose = purpose;
        BranchId = branchId;
        IsActive = true;

        AddDomainEvent(new LogoUploadedEvent(Id, FileName, ContentType, FileSizeBytes));
    }

    /// <summary>
    /// Reemplaza la imagen del logo con una nueva versión.
    /// </summary>
    public void Replace(string fileName, string contentType, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("El nombre de archivo del logo es requerido.");

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
            throw new DomainException($"Tipo de contenido inválido. Permitidos: {string.Join(", ", AllowedContentTypes)}.");

        if (data is null || data.Length == 0)
            throw new DomainException("Los datos del logo no pueden estar vacíos.");

        if (data.Length > MaxFileSizeBytes)
            throw new DomainException($"El logo supera el tamaño máximo permitido de {MaxFileSizeBytes / 1024 / 1024} MB.");

        FileName = fileName.Trim();
        ContentType = contentType.Trim().ToLowerInvariant();
        Data = data;
        FileSizeBytes = data.Length;

        AddDomainEvent(new LogoReplacedEvent(Id, FileName));
    }

    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("El logo ya está inactivo.");
        IsActive = false;
        AddDomainEvent(new LogoDeactivatedEvent(Id));
    }
}
