using System;

namespace PDV.Application.Common.Security;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeCommandAttribute : Attribute
{
    public string Permission { get; }

    public AuthorizeCommandAttribute(string permission)
    {
        Permission = permission;
    }
}
