using System;
using System.Collections.Generic;

namespace PDV.Application.Common.Helpers;

public static class RoleHelper
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string DeliveryMan = "DeliveryMan";
    public const string Telephonist = "Telephonist";
    public const string Almacen = "Almacen";
    public const string Compras = "Compras";
    public const string Picker = "Picker";
    public const string Verifier = "Verifier";

    public const string AdminDisplay = "Administrador";
    public const string ManagerDisplay = "Supervisor";
    public const string CashierDisplay = "Cajero/a";
    public const string DeliveryManDisplay = "Repartidor";
    public const string TelephonistDisplay = "Telefonista";
    public const string AlmacenDisplay = "Almacen";
    public const string ComprasDisplay = "Compras";
    public const string PickerDisplay = "Surtidor";
    public const string VerifierDisplay = "Verificador";

    /// <summary>
    /// Lista con todos los roles estándar y sus nombres de visualización en español.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleName, string DisplayName)> StandardRoles = new List<(string, string)>
    {
        (Admin, AdminDisplay),
        (Manager, ManagerDisplay),
        (Cashier, CashierDisplay),
        (DeliveryMan, DeliveryManDisplay),
        (Telephonist, TelephonistDisplay),
        (Almacen, AlmacenDisplay),
        (Compras, ComprasDisplay),
        (Picker, PickerDisplay),
        (Verifier, VerifierDisplay)
    };

    /// <summary>
    /// Obtiene el nombre del rol en español para mostrar en la interfaz de usuario.
    /// </summary>
    public static string GetRoleDisplayName(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return string.Empty;

        return role.Trim().ToLowerInvariant() switch
        {
            "admin" or "administrador" => AdminDisplay,
            "manager" or "supervisor" or "gerente" => ManagerDisplay,
            "cashier" or "cajero" or "cajera" or "cajero/a" => CashierDisplay,
            "deliveryman" or "repartidor" => DeliveryManDisplay,
            "telephonist" or "telefonista" => TelephonistDisplay,
            "almacen" or "almacén" or "warehouse" => AlmacenDisplay,
            "compras" or "purchasing" => ComprasDisplay,
            "picker" or "surtidor" or "surtidora" => PickerDisplay,
            "verifier" or "checker" or "verificador" or "verificadora" => VerifierDisplay,
            _ => role
        };
    }

    /// <summary>
    /// Convierte un nombre en español o variante al nombre canónico de rol en el sistema (ASP.NET Identity).
    /// </summary>
    public static string ToSystemRoleName(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return string.Empty;

        return role.Trim().ToLowerInvariant() switch
        {
            "admin" or "administrador" => Admin,
            "manager" or "supervisor" or "gerente" => Manager,
            "cashier" or "cajero" or "cajera" or "cajero/a" => Cashier,
            "deliveryman" or "repartidor" => DeliveryMan,
            "telephonist" or "telefonista" => Telephonist,
            "almacen" or "almacén" or "warehouse" => Almacen,
            "compras" or "purchasing" => Compras,
            "picker" or "surtidor" or "surtidora" => Picker,
            "verifier" or "checker" or "verificador" or "verificadora" => Verifier,
            _ => role
        };
    }

    /// <summary>
    /// Determina si un rol corresponde exclusivamente al rol de Surtidor (Picker).
    /// </summary>
    public static bool IsPickerRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return string.Equals(ToSystemRoleName(role), Picker, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determina si una lista o colección de roles incluye el rol de Surtidor (Picker).
    /// </summary>
    public static bool HasPickerRole(IEnumerable<string>? roles)
    {
        if (roles == null)
            return false;

        foreach (var r in roles)
        {
            if (IsPickerRole(r))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determina si un rol corresponde exclusivamente al rol de Repartidor (DeliveryMan).
    /// </summary>
    public static bool IsDeliveryManRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return string.Equals(ToSystemRoleName(role), DeliveryMan, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determina si una lista o colección de roles incluye el rol de Repartidor (DeliveryMan).
    /// </summary>
    public static bool HasDeliveryManRole(IEnumerable<string>? roles)
    {
        if (roles == null)
            return false;

        foreach (var r in roles)
        {
            if (IsDeliveryManRole(r))
                return true;
        }

        return false;
    }
}


