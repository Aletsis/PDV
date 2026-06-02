using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegisterIpAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "CashRegisters",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_IpAddress",
                table: "CashRegisters",
                column: "IpAddress",
                unique: true,
                filter: "\"IpAddress\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashRegisters_IpAddress",
                table: "CashRegisters");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "CashRegisters");
        }
    }
}
