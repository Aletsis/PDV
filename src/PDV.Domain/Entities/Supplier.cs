using System;
using System.Linq;
using PDV.Domain.Common;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

/// <summary>
/// Agregado raíz para Proveedores en el Punto de Venta.
/// Sincronizable con el catálogo de proveedores de CONTPAQi Comercial (TipoCliente = 3).
/// </summary>
public class Supplier : BaseEntity, IAggregateRoot
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string TaxId { get; private set; } // RFC
    public string Phone { get; private set; }
    public string Email { get; private set; }
    public Address? Address { get; private set; }
    public bool IsActive { get; private set; }
    public int? CommercialId { get; private set; }

#pragma warning disable CS8618
    private Supplier() { } // For EF Core
#pragma warning restore CS8618

    public Supplier(
        string code,
        string name,
        string taxId,
        string? phone = null,
        string? email = null,
        Address? address = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("El código del proveedor es obligatorio.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre o razón social del proveedor es obligatorio.");

        ValidateTaxId(taxId);
        ValidatePhone(phone);
        ValidateEmail(email);

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        TaxId = taxId?.Trim().ToUpperInvariant() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Address = address;
        IsActive = true;
    }

    private static void ValidateTaxId(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return;
        var trimmed = taxId.Trim();
        if (trimmed.Length < 10 || trimmed.Length > 13)
            throw new DomainException("El RFC debe contener entre 10 y 13 caracteres.");
    }

    private static void ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        var trimmed = phone.Trim();
        if (trimmed.Length < 10 || !trimmed.All(char.IsDigit))
            throw new DomainException("El teléfono debe contener al menos 10 dígitos numéricos.");
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var trimmed = email.Trim();
        if (!trimmed.Contains('@') || !trimmed.Contains('.'))
            throw new DomainException("El formato del correo electrónico es inválido.");
    }

    public void UpdateProfile(string name, string taxId, string? phone, string? email, Address? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del proveedor es obligatorio.");

        ValidateTaxId(taxId);
        ValidatePhone(phone);
        ValidateEmail(email);

        Name = name.Trim();
        TaxId = taxId?.Trim().ToUpperInvariant() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Address = address;
    }

    public void SetCommercialId(int commercialId)
    {
        CommercialId = commercialId;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
