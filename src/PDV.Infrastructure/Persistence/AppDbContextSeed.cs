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

                // 3. Crear el empleado del dominio correspondiente para que coincida con el usuario de Identity
                var employee = new Employee(
                    name: "Administrador del Sistema",
                    employeeCode: "EMP-ADMIN",
                    role: EmployeeRole.Admin,
                    userId: defaultUser.Id
                );

                context.Employees.Add(employee);
                await context.SaveChangesAsync();
            }
        }

        // 4. Sanar la base de datos de cualquier token de concurrencia nulo o incompatible
        if (context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                // Convertir tokens BLOB o NULL a Base64 TEXT válido en SQLite para que el ValueConverter los lea correctamente
                await context.Database.ExecuteSqlRawAsync("UPDATE Products SET RowVersion = '" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "' WHERE RowVersion IS NULL OR typeof(RowVersion) = 'blob';");
                await context.Database.ExecuteSqlRawAsync("UPDATE FolioSequences SET RowVersion = '" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "' WHERE RowVersion IS NULL OR typeof(RowVersion) = 'blob';");
                await context.Database.ExecuteSqlRawAsync("UPDATE TicketSequences SET RowVersion = '" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "' WHERE RowVersion IS NULL OR typeof(RowVersion) = 'blob';");

                // Sanar IDs y UUIDs a minúsculas en SQLite para evitar problemas de sensibilidad a mayúsculas
                var tablesToHeal = new[]
                {
                    ("Products", new[] { "Id", "BranchId" }),
                    ("Sales", new[] { "Id", "ShiftId", "ClientId", "CashRegisterId", "BranchId" }),
                    ("SaleItems", new[] { "Id", "SaleId", "ProductId" }),
                    ("Employees", new[] { "Id" }),
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
                            await context.Database.ExecuteSqlRawAsync($"UPDATE {table} SET {col} = lower({col}) WHERE {col} IS NOT NULL;");
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
            var productsWithNullRowVersion = await context.Products
                .IgnoreQueryFilters()
                .Where(p => p.RowVersion == null)
                .ToListAsync();

            if (productsWithNullRowVersion.Any())
            {
                foreach (var product in productsWithNullRowVersion)
                {
                    product.RowVersion = Guid.NewGuid().ToByteArray();
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
