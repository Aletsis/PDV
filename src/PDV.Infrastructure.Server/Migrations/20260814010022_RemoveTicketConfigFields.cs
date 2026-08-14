using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTicketConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoPrintTicket",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "PrintLogoOnTicket",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "TicketFooter",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "TicketHeader",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "TicketWidth",
                table: "SystemConfiguration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoPrintTicket",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrintLogoOnTicket",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TicketFooter",
                table: "SystemConfiguration",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketHeader",
                table: "SystemConfiguration",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketWidth",
                table: "SystemConfiguration",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
