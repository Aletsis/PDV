using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInventoryConceptMappingsPerBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryConceptMappings_Subtype",
                table: "InventoryConceptMappings");

            migrationBuilder.AlterColumn<int>(
                name: "Subtype",
                table: "InventoryConceptMappings",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "ConceptCode",
                table: "InventoryConceptMappings",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "BranchId",
                table: "InventoryConceptMappings",
                type: "TEXT",
                nullable: false,
                defaultValue: "00000000-0000-0000-0000-000000000000");

            migrationBuilder.AddColumn<string>(
                name: "DestinationBranchId",
                table: "InventoryConceptMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovementType",
                table: "InventoryConceptMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConceptMappings_BranchId_DestinationBranchId",
                table: "InventoryConceptMappings",
                columns: new[] { "BranchId", "DestinationBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConceptMappings_BranchId_MovementType_Subtype",
                table: "InventoryConceptMappings",
                columns: new[] { "BranchId", "MovementType", "Subtype" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConceptMappings_DestinationBranchId",
                table: "InventoryConceptMappings",
                column: "DestinationBranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryConceptMappings_BranchId_DestinationBranchId",
                table: "InventoryConceptMappings");

            migrationBuilder.DropIndex(
                name: "IX_InventoryConceptMappings_BranchId_MovementType_Subtype",
                table: "InventoryConceptMappings");

            migrationBuilder.DropIndex(
                name: "IX_InventoryConceptMappings_DestinationBranchId",
                table: "InventoryConceptMappings");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "InventoryConceptMappings");

            migrationBuilder.DropColumn(
                name: "DestinationBranchId",
                table: "InventoryConceptMappings");

            migrationBuilder.DropColumn(
                name: "MovementType",
                table: "InventoryConceptMappings");

            migrationBuilder.AlterColumn<int>(
                name: "Subtype",
                table: "InventoryConceptMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConceptCode",
                table: "InventoryConceptMappings",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConceptMappings_Subtype",
                table: "InventoryConceptMappings",
                column: "Subtype",
                unique: true);
        }
    }
}
