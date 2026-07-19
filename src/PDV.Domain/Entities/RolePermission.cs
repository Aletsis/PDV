using System;

namespace PDV.Domain.Entities;

public class RolePermission
{
    public string RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    
    public Permission Permission { get; private set; } = null!;

#pragma warning disable CS8618
    private RolePermission() { }
#pragma warning restore CS8618

    public RolePermission(string roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }
}
