using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

public class Client : BaseEntity, IAggregateRoot
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string TaxId { get; private set; } // RFC or similar
    public Address? Address { get; private set; }
    public string Phone { get; private set; }
    public string Email { get; private set; }
    /// <summary>Clasificación del cliente. Default: Retail (menudeo).</summary>
    public ClientType ClientType { get; private set; }
    public bool IsActive { get; private set; }

#pragma warning disable CS8618
    private Client() { } // For EF Core
#pragma warning restore CS8618

    public Client(
        string code,
        string name,
        string taxId,
        string phone,
        string email,
        ClientType clientType = ClientType.Retail)
    {
        if (string.IsNullOrWhiteSpace(code)) 
            throw new DomainException("El código del cliente es obligatorio.");
        if (string.IsNullOrWhiteSpace(name)) 
            throw new DomainException("El nombre del cliente es obligatorio.");
            
        ValidateTaxId(taxId);
        ValidatePhone(phone);
        ValidateEmail(email);
        
        Code = code.Trim();
        Name = name;
        TaxId = taxId?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        ClientType = clientType;
        IsActive = true;

        AddDomainEvent(new ClientRegisteredEvent(Id, Name));
    }

    private static void ValidateTaxId(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return; // Asumiendo que puede ser opcional
        
        var trimmed = taxId.Trim();
        if (trimmed.Length < 10 || trimmed.Length > 13)
            throw new DomainException("El RFC/TaxId debe tener entre 10 y 13 caracteres.");
    }

    private static void ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return; // Asumiendo que puede ser opcional
        
        var trimmed = phone.Trim();
        if (trimmed.Length < 10 || !trimmed.All(char.IsDigit))
            throw new DomainException("El teléfono debe contener al menos 10 dígitos.");
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return; // Asumiendo opcional
        
        var trimmed = email.Trim();
        if (!trimmed.Contains('@') || !trimmed.Contains('.'))
            throw new DomainException("El formato del correo electrónico es inválido.");
    }

    public void UpdateProfile(string name, string taxId)
    {
        if (string.IsNullOrWhiteSpace(name)) 
            throw new DomainException("El nombre del cliente es obligatorio.");
            
        ValidateTaxId(taxId);

        Name = name;
        TaxId = taxId?.Trim() ?? string.Empty;
        
        AddDomainEvent(new ClientProfileUpdatedEvent(Id, Name, TaxId));
    }

    public void ChangeCode(string newCode)
    {
        if (string.IsNullOrWhiteSpace(newCode)) 
            throw new DomainException("El código del cliente es obligatorio.");
        Code = newCode.Trim();
    }

    public void UpdateContactInfo(string phone, string email)
    {
        ValidatePhone(phone);
        ValidateEmail(email);

        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        
        AddDomainEvent(new ClientContactInfoUpdatedEvent(Id, Phone, Email));
    }

    public void UpdateAddress(Address address)
    {
        Address = address ?? throw new DomainException("La dirección no puede ser nula.");
        
        AddDomainEvent(new ClientAddressUpdatedEvent(Id));
    }



    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("El cliente ya está inactivo.");
        IsActive = false;
        AddDomainEvent(new ClientDeactivatedEvent(Id));
    }

    public void Activate()
    {
        if (IsActive) throw new DomainException("El cliente ya está activo.");
        IsActive = true;
        AddDomainEvent(new ClientActivatedEvent(Id));
    }

    public void ChangeClientType(ClientType newType)
    {
        if (ClientType == newType) return;
        ClientType = newType;
        AddDomainEvent(new ClientTypeChangedEvent(Id, ClientType));
    }
}

