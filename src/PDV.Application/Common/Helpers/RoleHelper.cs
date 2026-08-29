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

    public const string AdminDisplay = "Administrador";
    public const string ManagerDisplay = "Supervisor";
    public const string CashierDisplay = "Cajero/a";
    public const string DeliveryManDisplay = "Repartidor";
    public const string TelephonistDisplay = "Telefonista";
    public const string AlmacenDisplay = "Almacen";
    public const string ComprasDisplay = "Compras";

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
        (Compras, ComprasDisplay)
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
            _ => role
        };
    }
}
