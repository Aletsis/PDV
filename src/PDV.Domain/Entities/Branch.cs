using PDV.Domain.Common;
using PDV.Domain.Events;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

public class Branch : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string Code { get; private set; }
    public Address? Address { get; private set; }
    public string Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsMainBranch { get; private set; }

#pragma warning disable CS8618
    private Branch() { } // For EF Core
#pragma warning restore CS8618

    public Branch(
        string name, 
        string code, 
        Address? address, 
        string phone, 
        string? email = null,
        bool isMainBranch = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la sucursal es requerido.");
        
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("El código de la sucursal es requerido.");

        ValidatePhone(phone);
        ValidateEmail(email);

        Name = name.Trim();
        Code = code.Trim();
        Address = address;
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim();
        IsActive = true;
        IsMainBranch = isMainBranch;

        AddDomainEvent(new BranchCreatedEvent(Id, Name, Code));
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

    public void Update(string name, Address? address, string phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre es requerido.");

        ValidatePhone(phone);
        ValidateEmail(email);

        Name = name.Trim();
        Address = address;
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim();

        AddDomainEvent(new BranchUpdatedEvent(Id, Name));
    }

    public void Activate()
    {
        if (IsActive) throw new DomainException("La sucursal ya está activa.");
        IsActive = true;
        AddDomainEvent(new BranchActivatedEvent(Id));
    }
    
    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("La sucursal ya está inactiva.");
        if (IsMainBranch) throw new DomainException("No se puede desactivar la sucursal principal.");
        
        IsActive = false;
        AddDomainEvent(new BranchDeactivatedEvent(Id));
    }

    public void SetAsMainBranch()
    {
        if (IsMainBranch) return;
        IsMainBranch = true;
        AddDomainEvent(new BranchSetAsMainEvent(Id));
    }
}
