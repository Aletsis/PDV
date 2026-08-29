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
    [InlineData("Picker", "Surtidor")]
    [InlineData("picker", "Surtidor")]
    [InlineData("surtidor", "Surtidor")]
    [InlineData("Surtidor", "Surtidor")]
    [InlineData("surtidora", "Surtidor")]
    [InlineData("Verifier", "Verificador")]
    [InlineData("verifier", "Verificador")]
    [InlineData("verificador", "Verificador")]
    [InlineData("Verificador", "Verificador")]
    [InlineData("verificadora", "Verificador")]
    [InlineData("checker", "Verificador")]
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
    [InlineData("Surtidor", "Picker")]
    [InlineData("surtidor", "Picker")]
    [InlineData("surtidora", "Picker")]
    [InlineData("Picker", "Picker")]
    [InlineData("picker", "Picker")]
    [InlineData("Verificador", "Verifier")]
    [InlineData("verificador", "Verifier")]
    [InlineData("verificadora", "Verifier")]
    [InlineData("Verifier", "Verifier")]
    [InlineData("verifier", "Verifier")]
    [InlineData("checker", "Verifier")]
    public void ToSystemRoleName_ShouldReturnCanonicalIdentityRoleName(string inputRole, string expectedSystemRole)
    {
        // Act
        var result = RoleHelper.ToSystemRoleName(inputRole);

        // Assert
        Assert.Equal(expectedSystemRole, result);
    }

    [Fact]
    public void StandardRoles_ShouldContainAllNineRoles()
    {
        // Assert
        Assert.Equal(9, RoleHelper.StandardRoles.Count);
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Admin" && r.DisplayName == "Administrador");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Manager" && r.DisplayName == "Supervisor");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Cashier" && r.DisplayName == "Cajero/a");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "DeliveryMan" && r.DisplayName == "Repartidor");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Telephonist" && r.DisplayName == "Telefonista");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Almacen" && r.DisplayName == "Almacen");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Compras" && r.DisplayName == "Compras");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Picker" && r.DisplayName == "Surtidor");
        Assert.Contains(RoleHelper.StandardRoles, r => r.RoleName == "Verifier" && r.DisplayName == "Verificador");
    }

    [Theory]
    [InlineData("Picker", true)]
    [InlineData("picker", true)]
    [InlineData("Surtidor", true)]
    [InlineData("surtidor", true)]
    [InlineData("surtidora", true)]
    [InlineData("Almacen", false)]
    [InlineData("almacen", false)]
    [InlineData("almacén", false)]
    [InlineData("warehouse", false)]
    [InlineData("Admin", false)]
    [InlineData("Manager", false)]
    [InlineData("Cashier", false)]
    [InlineData("DeliveryMan", false)]
    [InlineData("Telephonist", false)]
    [InlineData("Compras", false)]
    [InlineData("Verifier", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPickerRole_ShouldCorrectlyIdentifyPickerRolesOnly(string? inputRole, bool expectedIsPicker)
    {
        // Act
        var result = RoleHelper.IsPickerRole(inputRole);

        // Assert
        Assert.Equal(expectedIsPicker, result);
    }

    [Fact]
    public void HasPickerRole_ShouldReturnTrue_WhenPickerIsInRolesList()
    {
        // Arrange
        var rolesWithPicker = new[] { "Almacen", "Picker" };
        var rolesWithSurtidor = new[] { "Cashier", "surtidor" };
        var rolesWithoutPicker = new[] { "Almacen", "Compras", "Manager" };

        // Act & Assert
        Assert.True(RoleHelper.HasPickerRole(rolesWithPicker));
        Assert.True(RoleHelper.HasPickerRole(rolesWithSurtidor));
        Assert.False(RoleHelper.HasPickerRole(rolesWithoutPicker));
        Assert.False(RoleHelper.HasPickerRole(null));
        Assert.False(RoleHelper.HasPickerRole(new string[0]));
    }
}

