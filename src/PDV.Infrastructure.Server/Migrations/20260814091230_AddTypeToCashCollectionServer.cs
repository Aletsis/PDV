using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTypeToCashCollectionServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "CashCollections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Migrar datos históricos basándonos en los prefijos existentes en Reason
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Type\" = 1 WHERE \"Reason\" LIKE '[INFLOW]%';");
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Type\" = 2 WHERE \"Reason\" LIKE '[OUTFLOW]%';");
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Type\" = 1 WHERE \"Type\" = 0;"); // Fallback

            // Limpiar prefijos de Reason
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Reason\" = REPLACE(\"Reason\", '[INFLOW] Morralla: ', '');");
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Reason\" = REPLACE(\"Reason\", '[OUTFLOW] Recolección: ', '');");
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Reason\" = REPLACE(\"Reason\", '[INFLOW] ', '');");
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Reason\" = REPLACE(\"Reason\", '[OUTFLOW] ', '');");

            // Limpiar sufijos de desglose si existen
            migrationBuilder.Sql("UPDATE \"CashCollections\" SET \"Reason\" = SUBSTRING(\"Reason\" FROM 1 FOR POSITION(' (Desglose:' IN \"Reason\") - 1) WHERE POSITION(' (Desglose:' IN \"Reason\") > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "CashCollections");
        }
    }
}
