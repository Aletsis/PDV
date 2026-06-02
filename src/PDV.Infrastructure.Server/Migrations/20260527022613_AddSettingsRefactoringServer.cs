using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsRefactoringServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlertCashLimit",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AlertLateOpening",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AlertLateOrder",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AlertSystemFailure",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoBackupEnabled",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AutoBackupTime",
                table: "SystemConfiguration",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoReportEnabled",
                table: "SystemConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AutoReportTime",
                table: "SystemConfiguration",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoReportUsers",
                table: "SystemConfiguration",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackupDirectory",
                table: "SystemConfiguration",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPassword",
                table: "SystemConfiguration",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "SystemConfiguration",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpServer",
                table: "SystemConfiguration",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUser",
                table: "SystemConfiguration",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketHeader",
                table: "SystemConfiguration",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertCashLimit",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AlertLateOpening",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AlertLateOrder",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AlertSystemFailure",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AutoBackupEnabled",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AutoBackupTime",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AutoReportEnabled",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AutoReportTime",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "AutoReportUsers",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "BackupDirectory",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SmtpPassword",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SmtpServer",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "SmtpUser",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "TicketHeader",
                table: "SystemConfiguration");
        }
    }
}
