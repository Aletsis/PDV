using System.Collections.Generic;
using PDV.Domain.Common;
using PDV.Domain.Exceptions;
using PDV.Domain.ValueObjects;

namespace PDV.Domain.Entities;

public class Company : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string RFC { get; private set; }
    public Address? FiscalAddress { get; private set; }
    public string Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<Branch> _branches = new();
    public IReadOnlyCollection<Branch> Branches => _branches.AsReadOnly();

#pragma warning disable CS8618
    private Company() { } // For EF Core
#pragma warning restore CS8618

    public Company(string name, string rfc, Address? fiscalAddress, string phone, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la empresa es requerido.");
        
        if (string.IsNullOrWhiteSpace(rfc))
            throw new DomainException("El RFC de la empresa es requerido.");

        Name = name.Trim();
        RFC = rfc.Trim().ToUpperInvariant();
        FiscalAddress = fiscalAddress;
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim();
        IsActive = true;
    }

    public void Update(string name, string rfc, Address? fiscalAddress, string phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre es requerido.");
        if (string.IsNullOrWhiteSpace(rfc))
            throw new DomainException("El RFC es requerido.");

        Name = name.Trim();
        RFC = rfc.Trim().ToUpperInvariant();
        FiscalAddress = fiscalAddress;
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim();
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
