using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PDV.Infrastructure.Persistence;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260829140000_AddBranchOrderSeriesServer")]
    public partial class AddBranchOrderSeriesServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderSeries",
                table: "Branches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderSeries",
                table: "Branches");
        }
    }
}
