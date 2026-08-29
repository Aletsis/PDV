using PDV.Application.Common.Helpers;
using Xunit;

namespace PDV.Tests.Security;

public class RoleHelperTests
{
    [Theory]
    [InlineData("Admin", "Administrador")]
    [InlineData("admin", "Administrador")]
    [InlineData("Administrador", "Administrador")]
    [InlineData("Manager", "Supervisor")]
    [InlineData("manager", "Supervisor")]
    [InlineData("Supervisor", "Supervisor")]
    [InlineData("supervisor", "Supervisor")]
    [InlineData("Cashier", "Cajero/a")]
    [InlineData("cashier", "Cajero/a")]
    [InlineData("Cajero/a", "Cajero/a")]
    [InlineData("cajero", "Cajero/a")]
    [InlineData("cajera", "Cajero/a")]
    [InlineData("DeliveryMan", "Repartidor")]
    [InlineData("deliveryman", "Repartidor")]
    [InlineData("repartidor", "Repartidor")]
    [InlineData("Telephonist", "Telefonista")]
    [InlineData("telephonist", "Telefonista")]
    [InlineData("telefonista", "Telefonista")]
    [InlineData("Almacen", "Almacen")]
    [InlineData("almacen", "Almacen")]
    [InlineData("almacén", "Almacen")]
    [InlineData("Compras", "Compras")]
    [InlineData("compras", "Compras")]
    public void GetRoleDisplayName_ShouldReturnExpectedSpanishName(string inputRole, string expectedDisplayName)
    {
        // Act
        var result = RoleHelper.GetRoleDisplayName(inputRole);

        // Assert
        Assert.Equal(expectedDisplayName, result);
    }

    [Theory]
    [InlineData("Administrador", "Admin")]
    [InlineData("administrador", "Admin")]
    [InlineData("Admin", "Admin")]
    [InlineData("Supervisor", "Manager")]
    [InlineData("supervisor", "Manager")]
    [InlineData("Manager", "Manager")]
    [InlineData("Cajero/a", "Cashier")]
    [InlineData("cajero", "Cashier")]
    [InlineData("cajera", "Cashier")]
    [InlineData("Cashier", "Cashier")]
    [InlineData("Repartidor", "DeliveryMan")]
    [InlineData("repartidor", "DeliveryMan")]
    [InlineData("DeliveryMan", "DeliveryMan")]
    [InlineData("Telefonista", "Telephonist")]
    [InlineData("telefonista", "Telephonist")]
    [InlineData("Telephonist", "Telephonist")]
    [InlineData("Almacen", "Almacen")]
    [InlineData("almacén", "Almacen")]
    [InlineData("Compras", "Compras")]
    public void ToSystemRoleName_ShouldReturnCanonicalIdentityRoleName(string inputRole, string expectedSystemRole)
    {
        // Act
        var result = RoleHelper.ToSystemRoleName(inputRole);

        // Assert
        Assert.Equal(expectedSystemRole, result);
    }

    [Fact]
    public void StandardRoles_ShouldContainAllSevenRoles()
    {
        // Assert
        Assert.Equal(7, RoleHelper.StandardRoles.Count);
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Admin" && r.DisplayName == "Administrador");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Manager" && r.DisplayName == "Supervisor");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Cashier" && r.DisplayName == "Cajero/a");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "DeliveryMan" && r.DisplayName == "Repartidor");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Telephonist" && r.DisplayName == "Telefonista");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Almacen" && r.DisplayName == "Almacen");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Compras" && r.DisplayName == "Compras");
    }
}
