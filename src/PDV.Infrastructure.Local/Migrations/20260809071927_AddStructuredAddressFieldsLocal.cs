using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredAddressFieldsLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FiscalAddress_Colony",
                table: "SystemConfiguration",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalAddress_ExteriorNumber",
                table: "SystemConfiguration",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalAddress_InteriorNumber",
                table: "SystemConfiguration",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalAddress_Colony",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalAddress_ExteriorNumber",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalAddress_InteriorNumber",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Colony",
                table: "Clients",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExteriorNumber",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InteriorNumber",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Colony",
                table: "Branches",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExteriorNumber",
                table: "Branches",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InteriorNumber",
                table: "Branches",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FiscalAddress_Colony",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "FiscalAddress_ExteriorNumber",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "FiscalAddress_InteriorNumber",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "FiscalAddress_Colony",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "FiscalAddress_ExteriorNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "FiscalAddress_InteriorNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Colony",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ExteriorNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "InteriorNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Colony",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ExteriorNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "InteriorNumber",
                table: "Branches");
        }
    }
}
