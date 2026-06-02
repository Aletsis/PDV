using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    /// <inheritdoc />
    public partial class AddIsReturnedToSaleAndSaleItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RowVersion",
                table: "TicketSequences",
                type: "TEXT",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturned",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturned",
                table: "SaleItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "RowVersion",
                table: "Products",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RowVersion",
                table: "FolioSequences",
                type: "TEXT",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldRowVersion: true,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReturned",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IsReturned",
                table: "SaleItems");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "TicketSequences",
                type: "BLOB",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "BLOB",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "FolioSequences",
                type: "BLOB",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldRowVersion: true,
                oldNullable: true);
        }
    }
}
