using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmployeeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cancellations_Employees_EmployeeId",
                table: "Cancellations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashCollections_Employees_EmployeeId",
                table: "CashCollections");

            migrationBuilder.DropForeignKey(
                name: "FK_CashCuts_Employees_EmployeeId",
                table: "CashCuts");

            migrationBuilder.DropForeignKey(
                name: "FK_CashRegisters_Employees_AssignedEmployeeId",
                table: "CashRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_Returns_Employees_EmployeeId",
                table: "Returns");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Returns_EmployeeId",
                table: "Returns");

            migrationBuilder.DropIndex(
                name: "IX_CashRegisters_AssignedEmployeeId",
                table: "CashRegisters");

            migrationBuilder.DropIndex(
                name: "IX_CashCuts_EmployeeId",
                table: "CashCuts");

            migrationBuilder.DropIndex(
                name: "IX_CashCollections_EmployeeId",
                table: "CashCollections");

            migrationBuilder.DropIndex(
                name: "IX_Cancellations_EmployeeId",
                table: "Cancellations");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Returns");

            migrationBuilder.DropColumn(
                name: "AssignedEmployeeId",
                table: "CashRegisters");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "CashCuts");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "CashCollections");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Cancellations");

            migrationBuilder.AddColumn<string>(
                name: "AssignedUserId",
                table: "CashRegisters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "CashRegisters");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedEmployeeId",
                table: "CashRegisters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "CashCuts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "CashCollections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Cancellations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    EmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Returns_EmployeeId",
                table: "Returns",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_AssignedEmployeeId",
                table: "CashRegisters",
                column: "AssignedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCuts_EmployeeId",
                table: "CashCuts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCollections_EmployeeId",
                table: "CashCollections",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Cancellations_EmployeeId",
                table: "Cancellations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cancellations_Employees_EmployeeId",
                table: "Cancellations",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashCollections_Employees_EmployeeId",
                table: "CashCollections",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashCuts_Employees_EmployeeId",
                table: "CashCuts",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashRegisters_Employees_AssignedEmployeeId",
                table: "CashRegisters",
                column: "AssignedEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Returns_Employees_EmployeeId",
                table: "Returns",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
