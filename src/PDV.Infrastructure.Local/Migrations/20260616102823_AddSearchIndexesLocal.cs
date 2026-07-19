using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexesLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_BranchId",
                table: "Sales");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchId_Date",
                table: "Sales",
                columns: new[] { "BranchId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name_GIN",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Plu",
                table: "Products",
                column: "Plu",
                filter: "\"Plu\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_BranchId_Date",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Name_GIN",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Plu",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchId",
                table: "Sales",
                column: "BranchId");
        }
    }
}
