using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBranchStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinStock",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Stock",
                table: "Products");

            migrationBuilder.Sql("DELETE FROM \"InventoryMovements\";");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ProductBranchStocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stock = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinStock = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBranchStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBranchStocks_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductBranchStocks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_BranchId",
                table: "InventoryMovements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBranchStocks_BranchId",
                table: "ProductBranchStocks",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBranchStocks_ProductId_BranchId",
                table: "ProductBranchStocks",
                columns: new[] { "ProductId", "BranchId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Branches_BranchId",
                table: "InventoryMovements",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_Branches_BranchId",
                table: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "ProductBranchStocks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_BranchId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "InventoryMovements");

            migrationBuilder.AddColumn<decimal>(
                name: "MinStock",
                table: "Products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Stock",
                table: "Products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
