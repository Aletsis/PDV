using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadMedida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComercialApiKey",
                table: "SystemConfiguration",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComercialApiUrl",
                table: "SystemConfiguration",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UnidadesMedida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<int>(type: "integer", nullable: false),
                    NombreUnidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abreviatura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Despliegue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClaveInt = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClaveSat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_UnidadesMedida", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesMedida_ExternalId",
                table: "UnidadesMedida",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnidadesMedida");

            migrationBuilder.DropColumn(
                name: "ComercialApiKey",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "ComercialApiUrl",
                table: "SystemConfiguration");
        }
    }
}
