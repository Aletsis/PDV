using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
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

            migrationBuilder.AddColumn<string>(
                name: "BranchId",
                table: "InventoryMovements",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProductBranchStocks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    BranchId = table.Column<string>(type: "TEXT", nullable: false),
                    Stock = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    MinStock = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    RowVersion = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
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
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RowVersion",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Stock",
                table: "Products",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
