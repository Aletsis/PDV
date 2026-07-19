using PDV.Domain.Common;

namespace PDV.Domain.Entities;

public class Permission : BaseEntity
{
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string Description { get; private set; }

#pragma warning disable CS8618
    private Permission() { }
#pragma warning restore CS8618

    public Permission(string name, string code, string description)
    {
        Name = name.Trim();
        Code = code.Trim().ToLowerInvariant();
        Description = description.Trim();
    }
}
