using PDV.Domain.Common;
using PDV.Domain.Enums;
using System;

namespace PDV.Domain.Entities;

public class TicketTemplate : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public TicketTemplateType TemplateType { get; private set; }
    public string ContentJson { get; private set; }
    public bool IsDefault { get; private set; }
    public Guid? PrinterId { get; private set; }

#pragma warning disable CS8618
    private TicketTemplate() { } // Para EF Core
#pragma warning restore CS8618

    public TicketTemplate(string name, TicketTemplateType templateType, string contentJson, bool isDefault = false, Guid? printerId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es requerido.", nameof(name));
        if (string.IsNullOrWhiteSpace(contentJson))
            throw new ArgumentException("El contenido JSON es requerido.", nameof(contentJson));

        Name = name.Trim();
        TemplateType = templateType;
        ContentJson = contentJson;
        IsDefault = isDefault;
        PrinterId = printerId;
    }

    public void Update(string name, string contentJson, bool isDefault, Guid? printerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es requerido.", nameof(name));
        if (string.IsNullOrWhiteSpace(contentJson))
            throw new ArgumentException("El contenido JSON es requerido.", nameof(contentJson));

        Name = name.Trim();
        ContentJson = contentJson;
        IsDefault = isDefault;
        PrinterId = printerId;
    }

    public void SetAsDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}
