using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
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
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ConceptCode",
                table: "InventoryConceptMappings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "InventoryConceptMappings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DestinationBranchId",
                table: "InventoryConceptMappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovementType",
                table: "InventoryConceptMappings",
                type: "integer",
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

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryConceptMappings_Branches_BranchId",
                table: "InventoryConceptMappings",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryConceptMappings_Branches_DestinationBranchId",
                table: "InventoryConceptMappings",
                column: "DestinationBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryConceptMappings_Branches_BranchId",
                table: "InventoryConceptMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryConceptMappings_Branches_DestinationBranchId",
                table: "InventoryConceptMappings");

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
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConceptCode",
                table: "InventoryConceptMappings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConceptMappings_Subtype",
                table: "InventoryConceptMappings",
                column: "Subtype",
                unique: true);
        }
    }
}
