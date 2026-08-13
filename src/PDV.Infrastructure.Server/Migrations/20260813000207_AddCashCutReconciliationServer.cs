using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCashCutReconciliationServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryConceptMappings_BranchId_DestinationBranchId",
                table: "InventoryConceptMappings");

            migrationBuilder.AddColumn<bool>(
                name: "IsReconciled",
                table: "Shifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconciledAt",
                table: "Shifts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReconciled",
                table: "CashCuts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconciledAt",
                table: "CashCuts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashCutReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashCutId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashRegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReconciledByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReconciliationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InitialCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CashSalesTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CardSalesTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InflowsTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OutflowsTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnsTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCardVouchers = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeliveredCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeliveredCardVouchers = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CashDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CardVouchersDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CashCutReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashCutReconciliations_CashCuts_CashCutId",
                        column: x => x.CashCutId,
                        principalTable: "CashCuts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashCutReconciliations_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashCutReconciliations_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashCutReconciliation_Denominations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashCutReconciliation_Denominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashCutReconciliation_Denominations_CashCutReconciliations_~",
                        column: x => x.ReconciliationId,
                        principalTable: "CashCutReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConceptMappings_BranchId_DestinationBranchId_Subty~",
                table: "InventoryConceptMappings",
                columns: new[] { "BranchId", "DestinationBranchId", "Subtype" });

            migrationBuilder.CreateIndex(
                name: "IX_CashCutReconciliation_Denominations_ReconciliationId",
                table: "CashCutReconciliation_Denominations",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCutReconciliations_CashCutId",
                table: "CashCutReconciliations",
                column: "CashCutId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCutReconciliations_CashRegisterId",
                table: "CashCutReconciliations",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCutReconciliations_ShiftId",
                table: "CashCutReconciliations",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashCutReconciliation_Denominations");

            migrationBuilder.DropTable(
                name: "CashCutReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_InventoryConceptMappings_BranchId_DestinationBranchId_Subty~",
                table: "InventoryConceptMappings");

            migrationBuilder.DropColumn(
                name: "IsReconciled",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ReconciledAt",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsReconciled",
                table: "CashCuts");

            migrationBuilder.DropColumn(
                name: "ReconciledAt",
                table: "CashCuts");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryConceptMappings_BranchId_DestinationBranchId",
                table: "InventoryConceptMappings",
                columns: new[] { "BranchId", "DestinationBranchId" });
        }
    }
}
