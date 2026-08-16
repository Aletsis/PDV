using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftIdToOrderServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Paso 1: Agregar columna como nullable para no violar constraints en datos existentes
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "Orders",
                type: "uuid",
                nullable: true,
                defaultValue: null);

            // Paso 2: Rellenar ShiftId en órdenes existentes con el turno más reciente de su caja
            migrationBuilder.Sql(@"
                UPDATE ""Orders"" o
                SET ""ShiftId"" = (
                    SELECT s.""Id""
                    FROM ""Shifts"" s
                    WHERE s.""CashRegisterId"" = o.""CashRegisterId""
                    ORDER BY s.""StartTime"" DESC
                    LIMIT 1
                )
                WHERE o.""ShiftId"" IS NULL;
            ");

            // Paso 3: Eliminar filas huérfanas (sin turno que corresponda) para no violar el FK
            migrationBuilder.Sql(@"
                DELETE FROM ""Orders"" WHERE ""ShiftId"" IS NULL;
            ");

            // Paso 4: Hacer la columna NOT NULL
            migrationBuilder.AlterColumn<Guid>(
                name: "ShiftId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Paso 5: Crear índice y FK
            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShiftId",
                table: "Orders",
                column: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Shifts_ShiftId",
                table: "Orders",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Shifts_ShiftId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ShiftId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "Orders");
        }
    }
}
