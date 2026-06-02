using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceFiscalDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Returns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Actualizar datos existentes antes de aplicar restricciones
            migrationBuilder.Sql("UPDATE \"Returns\" SET \"BranchId\" = s.\"BranchId\" FROM \"Sales\" s WHERE \"Returns\".\"SaleId\" = s.\"Id\";");
            migrationBuilder.Sql("UPDATE \"Returns\" SET \"BranchId\" = (SELECT \"Id\" FROM \"Branches\" LIMIT 1) WHERE \"BranchId\" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.AddColumn<string>(
                name: "CadenaOriginal",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoCertificadoEmisor",
                table: "Invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoCertificadoSAT",
                table: "Invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelloDigitalEmisor",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelloDigitalSAT",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Returns_BranchId",
                table: "Returns",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Returns_Branches_BranchId",
                table: "Returns",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Returns_Branches_BranchId",
                table: "Returns");

            migrationBuilder.DropIndex(
                name: "IX_Returns_BranchId",
                table: "Returns");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Returns");

            migrationBuilder.DropColumn(
                name: "CadenaOriginal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "NoCertificadoEmisor",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "NoCertificadoSAT",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SelloDigitalEmisor",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SelloDigitalSAT",
                table: "Invoices");
        }
    }
}
