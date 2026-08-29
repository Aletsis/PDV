using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PDV.Infrastructure.Persistence;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260829140000_AddBranchOrderSeriesLocal")]
    public partial class AddBranchOrderSeriesLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderSeries",
                table: "Branches",
                type: "TEXT",
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
