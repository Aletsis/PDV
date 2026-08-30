using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PDV.Infrastructure.Persistence;

#nullable disable

namespace PDV.Infrastructure.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260829220000_AdjustHistoricalTimestampsToLocalServer")]
    public partial class AdjustHistoricalTimestampsToLocalServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sales
            migrationBuilder.Sql(@"UPDATE ""Sales"" SET 
                ""Date"" = ""Date"" - INTERVAL '6 hours',
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // Shifts
            migrationBuilder.Sql(@"UPDATE ""Shifts"" SET 
                ""StartTime"" = ""StartTime"" - INTERVAL '6 hours',
                ""EndTime"" = CASE WHEN ""EndTime"" IS NOT NULL THEN ""EndTime"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // Orders
            migrationBuilder.Sql(@"UPDATE ""Orders"" SET 
                ""OrderDate"" = ""OrderDate"" - INTERVAL '6 hours',
                ""FulfillmentStartedAt"" = CASE WHEN ""FulfillmentStartedAt"" IS NOT NULL THEN ""FulfillmentStartedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""FilledAt"" = CASE WHEN ""FilledAt"" IS NOT NULL THEN ""FilledAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""VerifiedAt"" = CASE WHEN ""VerifiedAt"" IS NOT NULL THEN ""VerifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DispatchedAt"" = CASE WHEN ""DispatchedAt"" IS NOT NULL THEN ""DispatchedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeliveredAt"" = CASE WHEN ""DeliveredAt"" IS NOT NULL THEN ""DeliveredAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""SettledAt"" = CASE WHEN ""SettledAt"" IS NOT NULL THEN ""SettledAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // Invoices
            migrationBuilder.Sql(@"UPDATE ""Invoices"" SET 
                ""InvoiceDate"" = ""InvoiceDate"" - INTERVAL '6 hours',
                ""StampedAt"" = CASE WHEN ""StampedAt"" IS NOT NULL THEN ""StampedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CancelledAt"" = CASE WHEN ""CancelledAt"" IS NOT NULL THEN ""CancelledAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // CashCuts
            migrationBuilder.Sql(@"UPDATE ""CashCuts"" SET 
                ""CutDate"" = ""CutDate"" - INTERVAL '6 hours',
                ""ReconciledAt"" = CASE WHEN ""ReconciledAt"" IS NOT NULL THEN ""ReconciledAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // CashCollections
            migrationBuilder.Sql(@"UPDATE ""CashCollections"" SET 
                ""CollectionDate"" = ""CollectionDate"" - INTERVAL '6 hours',
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // CashCutReconciliations
            migrationBuilder.Sql(@"UPDATE ""CashCutReconciliations"" SET 
                ""ReconciliationDate"" = ""ReconciliationDate"" - INTERVAL '6 hours',
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // Returns
            migrationBuilder.Sql(@"UPDATE ""Returns"" SET 
                ""ReturnDate"" = ""ReturnDate"" - INTERVAL '6 hours',
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // Cancellations
            migrationBuilder.Sql(@"UPDATE ""Cancellations"" SET 
                ""CancellationDate"" = ""CancellationDate"" - INTERVAL '6 hours',
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // InventoryMovements
            migrationBuilder.Sql(@"UPDATE ""InventoryMovements"" SET 
                ""Date"" = ""Date"" - INTERVAL '6 hours',
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // InventoryDocuments
            migrationBuilder.Sql(@"UPDATE ""InventoryDocuments"" SET 
                ""Date"" = ""Date"" - INTERVAL '6 hours',
                ""LastAttemptAt"" = CASE WHEN ""LastAttemptAt"" IS NOT NULL THEN ""LastAttemptAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // DeliveryRoutes
            migrationBuilder.Sql(@"UPDATE ""DeliveryRoutes"" SET 
                ""CreatedDate"" = ""CreatedDate"" - INTERVAL '6 hours',
                ""DispatchedDate"" = CASE WHEN ""DispatchedDate"" IS NOT NULL THEN ""DispatchedDate"" - INTERVAL '6 hours' ELSE NULL END,
                ""SettledDate"" = CASE WHEN ""SettledDate"" IS NOT NULL THEN ""SettledDate"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // UserWorkStatuses
            migrationBuilder.Sql(@"UPDATE ""UserWorkStatuses"" SET 
                ""LastStatusChangeAt"" = ""LastStatusChangeAt"" - INTERVAL '6 hours',
                ""LastAssignedOrderAt"" = CASE WHEN ""LastAssignedOrderAt"" IS NOT NULL THEN ""LastAssignedOrderAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // ContpaqiSyncQueues
            migrationBuilder.Sql(@"UPDATE ""ContpaqiSyncQueues"" SET 
                ""EnqueuedAt"" = ""EnqueuedAt"" - INTERVAL '6 hours',
                ""LastAttemptAt"" = CASE WHEN ""LastAttemptAt"" IS NOT NULL THEN ""LastAttemptAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // InboxMessages
            migrationBuilder.Sql(@"UPDATE ""InboxMessages"" SET 
                ""ReceivedAt"" = ""ReceivedAt"" - INTERVAL '6 hours',
                ""ProcessedAt"" = CASE WHEN ""ProcessedAt"" IS NOT NULL THEN ""ProcessedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // OutboxMessages
            migrationBuilder.Sql(@"UPDATE ""OutboxMessages"" SET 
                ""EnqueuedAt"" = ""EnqueuedAt"" - INTERVAL '6 hours',
                ""LastAttemptAt"" = CASE WHEN ""LastAttemptAt"" IS NOT NULL THEN ""LastAttemptAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");

            // AuditLogs
            migrationBuilder.Sql(@"UPDATE ""AuditLogs"" SET 
                ""Timestamp"" = ""Timestamp"" - INTERVAL '6 hours',
                ""CreatedAt"" = ""CreatedAt"" - INTERVAL '6 hours',
                ""LastModifiedAt"" = CASE WHEN ""LastModifiedAt"" IS NOT NULL THEN ""LastModifiedAt"" - INTERVAL '6 hours' ELSE NULL END,
                ""DeletedAt"" = CASE WHEN ""DeletedAt"" IS NOT NULL THEN ""DeletedAt"" - INTERVAL '6 hours' ELSE NULL END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert +6 hours if rolled back
            migrationBuilder.Sql(@"UPDATE ""Sales"" SET ""Date"" = ""Date"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""Shifts"" SET ""StartTime"" = ""StartTime"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""Orders"" SET ""OrderDate"" = ""OrderDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""Invoices"" SET ""InvoiceDate"" = ""InvoiceDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""CashCuts"" SET ""CutDate"" = ""CutDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""CashCollections"" SET ""CollectionDate"" = ""CollectionDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""CashCutReconciliations"" SET ""ReconciliationDate"" = ""ReconciliationDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""Returns"" SET ""ReturnDate"" = ""ReturnDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""Cancellations"" SET ""CancellationDate"" = ""CancellationDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""InventoryMovements"" SET ""Date"" = ""Date"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""InventoryDocuments"" SET ""Date"" = ""Date"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""DeliveryRoutes"" SET ""CreatedDate"" = ""CreatedDate"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""UserWorkStatuses"" SET ""LastStatusChangeAt"" = ""LastStatusChangeAt"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
            migrationBuilder.Sql(@"UPDATE ""AuditLogs"" SET ""Timestamp"" = ""Timestamp"" + INTERVAL '6 hours', ""CreatedAt"" = ""CreatedAt"" + INTERVAL '6 hours';");
        }
    }
}
