using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
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
                name: "EmployeeId",
                table: "CashCuts");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "CashCollections");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Cancellations");

            migrationBuilder.RenameColumn(
                name: "AssignedEmployeeId",
                table: "CashRegisters",
                newName: "AssignedUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AssignedUserId",
                table: "CashRegisters",
                newName: "AssignedEmployeeId");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "Returns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "CashCuts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "CashCollections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "Cancellations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    EmployeeCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true)
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
