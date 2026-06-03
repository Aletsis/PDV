using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    /// <inheritdoc />
    public partial class FixOwnedEntitiesKeysServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Shifts_SalesTaxTotals",
                table: "Shifts_SalesTaxTotals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shifts_ReturnsTaxTotals",
                table: "Shifts_ReturnsTaxTotals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shifts_PaymentMethodTotals",
                table: "Shifts_PaymentMethodTotals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShiftCreditNote",
                table: "ShiftCreditNote");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SaleTaxBreakdowns",
                table: "SaleTaxBreakdowns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReturnTaxBreakdowns",
                table: "ReturnTaxBreakdowns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InvoiceTaxBreakdowns",
                table: "InvoiceTaxBreakdowns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CashCuts_DeclaredVouchers",
                table: "CashCuts_DeclaredVouchers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CashCuts_CashDenominations",
                table: "CashCuts_CashDenominations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CashCollections_Denominations",
                table: "CashCollections_Denominations");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shifts_SalesTaxTotals",
                table: "Shifts_SalesTaxTotals",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shifts_ReturnsTaxTotals",
                table: "Shifts_ReturnsTaxTotals",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shifts_PaymentMethodTotals",
                table: "Shifts_PaymentMethodTotals",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShiftCreditNote",
                table: "ShiftCreditNote",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SaleTaxBreakdowns",
                table: "SaleTaxBreakdowns",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReturnTaxBreakdowns",
                table: "ReturnTaxBreakdowns",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InvoiceTaxBreakdowns",
                table: "InvoiceTaxBreakdowns",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CashCuts_DeclaredVouchers",
                table: "CashCuts_DeclaredVouchers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CashCuts_CashDenominations",
                table: "CashCuts_CashDenominations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CashCollections_Denominations",
                table: "CashCollections_Denominations",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_SalesTaxTotals_ShiftId",
                table: "Shifts_SalesTaxTotals",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ReturnsTaxTotals_ShiftId",
                table: "Shifts_ReturnsTaxTotals",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_PaymentMethodTotals_ShiftId",
                table: "Shifts_PaymentMethodTotals",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCreditNote_ShiftId",
                table: "ShiftCreditNote",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleTaxBreakdowns_SaleId",
                table: "SaleTaxBreakdowns",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTaxBreakdowns_ReturnId",
                table: "ReturnTaxBreakdowns",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTaxBreakdowns_InvoiceId",
                table: "InvoiceTaxBreakdowns",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCuts_DeclaredVouchers_CashCutId",
                table: "CashCuts_DeclaredVouchers",
                column: "CashCutId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCuts_CashDenominations_CashCutId",
                table: "CashCuts_CashDenominations",
                column: "CashCutId");

            migrationBuilder.CreateIndex(
                name: "IX_CashCollections_Denominations_CashCollectionId",
                table: "CashCollections_Denominations",
                column: "CashCollectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Shifts_SalesTaxTotals",
                table: "Shifts_SalesTaxTotals");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_SalesTaxTotals_ShiftId",
                table: "Shifts_SalesTaxTotals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shifts_ReturnsTaxTotals",
                table: "Shifts_ReturnsTaxTotals");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_ReturnsTaxTotals_ShiftId",
                table: "Shifts_ReturnsTaxTotals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shifts_PaymentMethodTotals",
                table: "Shifts_PaymentMethodTotals");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_PaymentMethodTotals_ShiftId",
                table: "Shifts_PaymentMethodTotals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShiftCreditNote",
                table: "ShiftCreditNote");

            migrationBuilder.DropIndex(
                name: "IX_ShiftCreditNote_ShiftId",
                table: "ShiftCreditNote");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SaleTaxBreakdowns",
                table: "SaleTaxBreakdowns");

            migrationBuilder.DropIndex(
                name: "IX_SaleTaxBreakdowns_SaleId",
                table: "SaleTaxBreakdowns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReturnTaxBreakdowns",
                table: "ReturnTaxBreakdowns");

            migrationBuilder.DropIndex(
                name: "IX_ReturnTaxBreakdowns_ReturnId",
                table: "ReturnTaxBreakdowns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InvoiceTaxBreakdowns",
                table: "InvoiceTaxBreakdowns");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceTaxBreakdowns_InvoiceId",
                table: "InvoiceTaxBreakdowns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CashCuts_DeclaredVouchers",
                table: "CashCuts_DeclaredVouchers");

            migrationBuilder.DropIndex(
                name: "IX_CashCuts_DeclaredVouchers_CashCutId",
                table: "CashCuts_DeclaredVouchers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CashCuts_CashDenominations",
                table: "CashCuts_CashDenominations");

            migrationBuilder.DropIndex(
                name: "IX_CashCuts_CashDenominations_CashCutId",
                table: "CashCuts_CashDenominations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CashCollections_Denominations",
                table: "CashCollections_Denominations");

            migrationBuilder.DropIndex(
                name: "IX_CashCollections_Denominations_CashCollectionId",
                table: "CashCollections_Denominations");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shifts_SalesTaxTotals",
                table: "Shifts_SalesTaxTotals",
                columns: new[] { "ShiftId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shifts_ReturnsTaxTotals",
                table: "Shifts_ReturnsTaxTotals",
                columns: new[] { "ShiftId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shifts_PaymentMethodTotals",
                table: "Shifts_PaymentMethodTotals",
                columns: new[] { "ShiftId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShiftCreditNote",
                table: "ShiftCreditNote",
                columns: new[] { "ShiftId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_SaleTaxBreakdowns",
                table: "SaleTaxBreakdowns",
                columns: new[] { "SaleId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReturnTaxBreakdowns",
                table: "ReturnTaxBreakdowns",
                columns: new[] { "ReturnId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_InvoiceTaxBreakdowns",
                table: "InvoiceTaxBreakdowns",
                columns: new[] { "InvoiceId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CashCuts_DeclaredVouchers",
                table: "CashCuts_DeclaredVouchers",
                columns: new[] { "CashCutId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CashCuts_CashDenominations",
                table: "CashCuts_CashDenominations",
                columns: new[] { "CashCutId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CashCollections_Denominations",
                table: "CashCollections_Denominations",
                columns: new[] { "CashCollectionId", "Id" });
        }
    }
}
