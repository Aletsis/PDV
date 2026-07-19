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
        var roles = new[] { "Admin", "Manager", "Cashier" };
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

            var result = await userManager.CreateAsync(defaultUser, "admin");
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
            new Permission("Realizar Retiro de Efectivo", "sales.cash_collection", "Permite realizar retiros o cobros de efectivo de la caja")
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
