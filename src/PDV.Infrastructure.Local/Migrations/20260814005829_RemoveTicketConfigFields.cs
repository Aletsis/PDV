using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrintLogoOnTicket",
                table: "SystemConfiguration",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TicketFooter",
                table: "SystemConfiguration",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketHeader",
                table: "SystemConfiguration",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketWidth",
                table: "SystemConfiguration",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
