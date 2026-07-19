using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCfdiBillingFieldsServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CsdCertificateData",
                table: "SystemConfiguration",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CsdPassword",
                table: "SystemConfiguration",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CsdPrivateKeyData",
                table: "SystemConfiguration",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PacApiKey",
                table: "SystemConfiguration",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverFiscalRegime",
                table: "Invoices",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "616");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverZipCode",
                table: "Invoices",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "00000");

            migrationBuilder.AddColumn<string>(
                name: "FiscalRegime",
                table: "Clients",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalZipCode",
                table: "Clients",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CsdCertificateData",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CsdPassword",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CsdPrivateKeyData",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "PacApiKey",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "ReceiverFiscalRegime",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReceiverZipCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FiscalRegime",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalZipCode",
                table: "Clients");
        }
    }
}
