using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Enums;
using PDV.Infrastructure.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PDV.Infrastructure.Persistence;

public static class AppDbContextSeed
{
    public static async Task SeedDefaultUserAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext context)
    {
        // 1. Asegurar la existencia de los roles principales
        var roles = new[] { "Admin", "Manager", "Cashier", "DeliveryMan", "Telephonist", "Almacen", "Compras", "Picker", "Verifier" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Si no hay usuarios en la base de datos, crear el administrador inicial
        if (!userManager.Users.Any())
        {
            var defaultUser = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "admin",
                Email = "admin@pdv.com",
                FullName = "Administrador del Sistema",
                IsActive = true
            };

            var result = await userManager.CreateAsync(defaultUser, "Admin123!");
            if (result.Succeeded)
            {
                // Asignar el rol de Admin
                await userManager.AddToRoleAsync(defaultUser, "Admin");
            }
        }

        // 3. Sembrar los permisos del sistema si no existen
        var permissions = new[]
        {
            new Permission("Cancelar Venta Completa", "sales.cancel", "Permite cancelar una venta completa activa"),
            new Permission("Eliminar/Cancelar Artículo de Venta", "sales.cancel_item", "Permite eliminar o cancelar un artículo de la venta"),
            new Permission("Modificar Precio / Descuento de Artículo", "sales.override_price", "Permite modificar el precio original de un artículo o aplicar descuento"),
            new Permission("Procesar Devolución / Reembolso", "sales.refund", "Permite procesar la devolución de mercancía de una venta pagada"),
            new Permission("Realizar Corte de Caja", "sales.cash_cut", "Permite realizar el corte de caja / cierre de turno"),
            new Permission("Realizar Retiro de Efectivo", "sales.cash_collection", "Permite realizar retiros o cobros de efectivo de la caja"),
            new Permission("Crear/Editar Clientes", "clients.create_edit", "Permite crear y editar información de clientes"),
            new Permission("Consultar Catálogo de Productos", "products.view_catalog", "Permite ver la lista de productos en modo consulta"),
            new Permission("Capturar Pedidos", "orders.capture", "Permite capturar nuevos pedidos en caja"),
            new Permission("Tomar Pedidos Telefónicos", "orders.take", "Permite tomar y capturar pedidos preliminares vía telefónica"),
            new Permission("Surtir Pedidos", "orders.fulfill", "Permite surtir pedidos y marcarlos como surtidos en almacén"),
            new Permission("Verificar Pedidos", "orders.verify", "Permite verificar pedidos surtidos y registrar pesaje real"),
            new Permission("Entregar Pedidos en Ruta", "orders.deliver", "Permite ver ruta asignada y marcar entrega o incidencia de pedidos"),
            new Permission("Gestionar Rutas de Reparto", "orders.routes", "Permite crear, despachar y gestionar rutas de reparto"),
            new Permission("Liquidar Cuentas de Ruta", "orders.settle", "Permite realizar la liquidación de cuentas de rutas de reparto"),
            new Permission("Gestionar Zonas de Reparto", "delivery_zones.manage", "Permite configurar zonas de reparto en el mapa"),
            new Permission("Conciliar Corte de Caja", "sales.reconcile_cash_cut", "Permite validar y conciliar el efectivo y váuchers del corte de caja")
        };

        foreach (var p in permissions)
        {
            var exists = await context.Permissions.AnyAsync(x => x.Code == p.Code);
            if (!exists)
            {
                context.Permissions.Add(p);
            }
        }
        await context.SaveChangesAsync();

        // 4. Mapear permisos a roles Admin y Manager
        var adminRole = await roleManager.FindByNameAsync("Admin");
        var managerRole = await roleManager.FindByNameAsync("Manager");

        if (adminRole != null)
        {
            var dbPermissions = await context.Permissions.ToListAsync();
            foreach (var p in dbPermissions)
            {
                var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == adminRole.Id && rp.PermissionId == p.Id);
                if (!roleHasPerm)
                {
                    context.RolePermissions.Add(new RolePermission(adminRole.Id, p.Id));
                }
            }
        }

        if (managerRole != null)
        {
            var dbPermissions = await context.Permissions.ToListAsync();
            foreach (var p in dbPermissions)
            {
                var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == managerRole.Id && rp.PermissionId == p.Id);
                if (!roleHasPerm)
                {
                    context.RolePermissions.Add(new RolePermission(managerRole.Id, p.Id));
                }
            }
        }

        var telephonistRole = await roleManager.FindByNameAsync("Telephonist");
        if (telephonistRole != null)
        {
            var telephonistPermissionCodes = new[] { "products.view_catalog", "clients.create_edit", "orders.capture", "orders.take" };
            var dbPermissions = await context.Permissions
                .Where(p => telephonistPermissionCodes.Contains(p.Code))
                .ToListAsync();

            foreach (var p in dbPermissions)
            {
                var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == telephonistRole.Id && rp.PermissionId == p.Id);
                if (!roleHasPerm)
                {
                    context.RolePermissions.Add(new RolePermission(telephonistRole.Id, p.Id));
                }
            }
        }

        var deliveryManRole = await roleManager.FindByNameAsync("DeliveryMan");
        if (deliveryManRole != null)
        {
            var deliveryPermCodes = new[] { "orders.deliver" };
            var dbPermissions = await context.Permissions
                .Where(p => deliveryPermCodes.Contains(p.Code))
                .ToListAsync();

            foreach (var p in dbPermissions)
            {
                var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == deliveryManRole.Id && rp.PermissionId == p.Id);
                if (!roleHasPerm)
                {
                    context.RolePermissions.Add(new RolePermission(deliveryManRole.Id, p.Id));
                }
            }
        }

        var cashierRole = await roleManager.FindByNameAsync("Cashier");
        if (cashierRole != null)
        {
            var cashierPermCodes = new[] { "orders.capture", "orders.verify", "orders.routes", "orders.settle", "products.view_catalog", "clients.create_edit" };
            var dbPermissions = await context.Permissions
                .Where(p => cashierPermCodes.Contains(p.Code))
                .ToListAsync();

            foreach (var p in dbPermissions)
            {
                var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == cashierRole.Id && rp.PermissionId == p.Id);
                if (!roleHasPerm)
                {
                    context.RolePermissions.Add(new RolePermission(cashierRole.Id, p.Id));
                }
            }
        }

        var catalogViewerRoles = new[] { "Almacen", "Compras" };
        foreach (var rName in catalogViewerRoles)
        {
            var role = await roleManager.FindByNameAsync(rName);
            if (role != null)
            {
                var catalogPermCodes = rName == "Almacen" 
                    ? new[] { "products.view_catalog", "orders.fulfill" } 
                    : new[] { "products.view_catalog" };

                var dbPermissions = await context.Permissions
                    .Where(p => catalogPermCodes.Contains(p.Code))
                    .ToListAsync();

                foreach (var p in dbPermissions)
                {
                    var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == p.Id);
                    if (!roleHasPerm)
                    {
                        context.RolePermissions.Add(new RolePermission(role.Id, p.Id));
                    }
                }
            }
        }

        var pickerRole = await roleManager.FindByNameAsync("Picker");
        if (pickerRole != null)
        {
            var pickerPermCodes = new[] { "products.view_catalog", "orders.fulfill" };
            var dbPermissions = await context.Permissions
                .Where(p => pickerPermCodes.Contains(p.Code))
                .ToListAsync();

            foreach (var p in dbPermissions)
            {
                var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == pickerRole.Id && rp.PermissionId == p.Id);
                if (!roleHasPerm)
                {
                    context.RolePermissions.Add(new RolePermission(pickerRole.Id, p.Id));
                }
            }
        }

        var verifierRole = await roleManager.FindByNameAsync("Verifier");
        if (verifierRole != null)
        {
            var verifierPermCodes = new[] { "products.view_catalog", "clients.create_edit", "orders.capture", "orders.verify" };
            var dbPermissions = await context.Permissions
                .Where(p => verifierPermCodes.Contains(p.Code))
                .ToListAsync();

            foreach (var p in dbPermissions)
            {
                var roleHasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == verifierRole.Id && rp.PermissionId == p.Id);
                if (!roleHasPerm)
                {
                    context.RolePermissions.Add(new RolePermission(verifierRole.Id, p.Id));
                }
            }
        }
        await context.SaveChangesAsync();

        // 4. Sanar la base de datos de cualquier token de concurrencia nulo o incompatible
        if (context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                // Convertir tokens BLOB o NULL a Base64 TEXT válido en SQLite para que el ValueConverter los lea correctamente
                await context.Database.ExecuteSqlRawAsync("UPDATE ProductBranchStocks SET RowVersion = '" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "' WHERE RowVersion IS NULL OR typeof(RowVersion) = 'blob';");
                await context.Database.ExecuteSqlRawAsync("UPDATE FolioSequences SET RowVersion = '" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "' WHERE RowVersion IS NULL OR typeof(RowVersion) = 'blob';");
                await context.Database.ExecuteSqlRawAsync("UPDATE TicketSequences SET RowVersion = '" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "' WHERE RowVersion IS NULL OR typeof(RowVersion) = 'blob';");

                // Sanar IDs y UUIDs a minúsculas en SQLite para evitar problemas de sensibilidad a mayúsculas
                var tablesToHeal = new[]
                {
                    ("Products", new[] { "Id", "BranchId" }),
                    ("ProductBranchStocks", new[] { "Id", "ProductId", "BranchId" }),
                    ("Sales", new[] { "Id", "ShiftId", "ClientId", "CashRegisterId", "BranchId" }),
                    ("SaleItems", new[] { "Id", "SaleId", "ProductId" }),

                    ("CashRegisters", new[] { "Id", "BranchId" }),
                    ("CashCuts", new[] { "Id", "ShiftId", "CashRegisterId", "CashierId" }),
                    ("CashCollections", new[] { "Id", "CashRegisterId", "CashierId" }),
                    ("Cancellations", new[] { "Id", "SaleId", "SaleItemId" }),
                    ("Returns", new[] { "Id", "SaleId" }),
                    ("Clients", new[] { "Id" }),
                    ("Invoices", new[] { "Id", "SaleId" }),
                    ("Logos", new[] { "Id", "BranchId" }),
                    ("Printers", new[] { "Id", "BranchId" }),
                    ("Branches", new[] { "Id" }),
                    ("SystemConfigurations", new[] { "Id" }),
                    ("FolioSequences", new[] { "Id", "CashRegisterId" }),
                    ("TicketSequences", new[] { "Id", "CashRegisterId" }),
                    ("Shifts", new[] { "Id", "CashRegisterId" }),
                    ("OutboxMessages", new[] { "Id" }),
                    ("InventoryMovements", new[] { "Id", "ProductId", "SaleId" })
                };

                foreach (var (table, columns) in tablesToHeal)
                {
                    foreach (var col in columns)
                    {
                        try
                        {
#pragma warning disable EF1002
                            await context.Database.ExecuteSqlRawAsync($"UPDATE {table} SET {col} = lower({col}) WHERE {col} IS NOT NULL;");
#pragma warning restore EF1002
                        }
                        catch {}
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al reparar base de datos SQLite: {ex.Message}");
            }
        }
        else
        {
            var stocksWithNullRowVersion = await context.ProductBranchStocks
                .IgnoreQueryFilters()
                .Where(p => p.RowVersion == null)
                .ToListAsync();

            if (stocksWithNullRowVersion.Any())
            {
                foreach (var stock in stocksWithNullRowVersion)
                {
                    stock.RowVersion = Guid.NewGuid().ToByteArray();
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
