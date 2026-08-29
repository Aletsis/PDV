using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PDV.Infrastructure.Persistence;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260828233000_ExpandOrderLifecycleServer")]
    public partial class ExpandOrderLifecycleServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DeliveryManId",
                table: "DeliveryRoutes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedById",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettledById",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralNotes",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNotes",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Orders",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutOfZone",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfillmentStartedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FilledAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedQuantity",
                table: "OrderItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "OrderItems",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFulfilled",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VerifiedById", table: "Orders");
            migrationBuilder.DropColumn(name: "SettledById", table: "Orders");
            migrationBuilder.DropColumn(name: "GeneralNotes", table: "Orders");
            migrationBuilder.DropColumn(name: "DeliveryNotes", table: "Orders");
            migrationBuilder.DropColumn(name: "CancellationReason", table: "Orders");
            migrationBuilder.DropColumn(name: "IsOutOfZone", table: "Orders");
            migrationBuilder.DropColumn(name: "FulfillmentStartedAt", table: "Orders");
            migrationBuilder.DropColumn(name: "FilledAt", table: "Orders");
            migrationBuilder.DropColumn(name: "VerifiedAt", table: "Orders");
            migrationBuilder.DropColumn(name: "DispatchedAt", table: "Orders");
            migrationBuilder.DropColumn(name: "DeliveredAt", table: "Orders");
            migrationBuilder.DropColumn(name: "SettledAt", table: "Orders");

            migrationBuilder.DropColumn(name: "RequestedQuantity", table: "OrderItems");
            migrationBuilder.DropColumn(name: "Notes", table: "OrderItems");
            migrationBuilder.DropColumn(name: "IsFulfilled", table: "OrderItems");
        }
    }
}
