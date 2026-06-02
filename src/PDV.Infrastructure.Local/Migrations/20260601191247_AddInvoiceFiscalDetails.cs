using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceFiscalDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchId",
                table: "Returns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CadenaOriginal",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoCertificadoEmisor",
                table: "Invoices",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoCertificadoSAT",
                table: "Invoices",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelloDigitalEmisor",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelloDigitalSAT",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConceptCode",
                table: "FolioSequences",
                type: "TEXT",
                maxLength: 50,
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

            migrationBuilder.DropColumn(
                name: "ConceptCode",
                table: "FolioSequences");
        }
    }
}
