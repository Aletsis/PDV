using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    /// <inheritdoc />
    public partial class AddCfdiBillingFieldsLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CsdCertificateData",
                table: "SystemConfiguration",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CsdPassword",
                table: "SystemConfiguration",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CsdPrivateKeyData",
                table: "SystemConfiguration",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PacApiKey",
                table: "SystemConfiguration",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverFiscalRegime",
                table: "Invoices",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "616");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverZipCode",
                table: "Invoices",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "00000");

            migrationBuilder.AddColumn<string>(
                name: "FiscalRegime",
                table: "Clients",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalZipCode",
                table: "Clients",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncConflicts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ServerValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ConflictType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Resolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolutionStrategy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncConflicts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_MessageId",
                table: "InboxMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_State_ReceivedAt",
                table: "InboxMessages",
                columns: new[] { "State", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_EntityName_EntityId",
                table: "SyncConflicts",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncConflicts_Resolved",
                table: "SyncConflicts",
                column: "Resolved");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "SyncConflicts");

            migrationBuilder.DropColumn(
                name: "CsdCertificateData",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CsdPassword",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "CsdPrivateKeyData",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "PacApiKey",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "ReceiverFiscalRegime",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReceiverZipCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FiscalRegime",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalZipCode",
                table: "Clients");
        }
    }
}
