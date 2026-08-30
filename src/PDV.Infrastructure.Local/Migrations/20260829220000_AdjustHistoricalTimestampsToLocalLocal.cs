using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PDV.Infrastructure.Persistence;

#nullable disable

namespace PDV.Infrastructure.Local.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260829220000_AdjustHistoricalTimestampsToLocalLocal")]
    public partial class AdjustHistoricalTimestampsToLocalLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sales
            migrationBuilder.Sql(@"UPDATE Sales SET 
                Date = datetime(Date, '-6 hours'),
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // Shifts
            migrationBuilder.Sql(@"UPDATE Shifts SET 
                StartTime = datetime(StartTime, '-6 hours'),
                EndTime = CASE WHEN EndTime IS NOT NULL THEN datetime(EndTime, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // Orders
            migrationBuilder.Sql(@"UPDATE Orders SET 
                OrderDate = datetime(OrderDate, '-6 hours'),
                FulfillmentStartedAt = CASE WHEN FulfillmentStartedAt IS NOT NULL THEN datetime(FulfillmentStartedAt, '-6 hours') ELSE NULL END,
                FilledAt = CASE WHEN FilledAt IS NOT NULL THEN datetime(FilledAt, '-6 hours') ELSE NULL END,
                VerifiedAt = CASE WHEN VerifiedAt IS NOT NULL THEN datetime(VerifiedAt, '-6 hours') ELSE NULL END,
                DispatchedAt = CASE WHEN DispatchedAt IS NOT NULL THEN datetime(DispatchedAt, '-6 hours') ELSE NULL END,
                DeliveredAt = CASE WHEN DeliveredAt IS NOT NULL THEN datetime(DeliveredAt, '-6 hours') ELSE NULL END,
                SettledAt = CASE WHEN SettledAt IS NOT NULL THEN datetime(SettledAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // Invoices
            migrationBuilder.Sql(@"UPDATE Invoices SET 
                InvoiceDate = datetime(InvoiceDate, '-6 hours'),
                StampedAt = CASE WHEN StampedAt IS NOT NULL THEN datetime(StampedAt, '-6 hours') ELSE NULL END,
                CancelledAt = CASE WHEN CancelledAt IS NOT NULL THEN datetime(CancelledAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // CashCuts
            migrationBuilder.Sql(@"UPDATE CashCuts SET 
                CutDate = datetime(CutDate, '-6 hours'),
                ReconciledAt = CASE WHEN ReconciledAt IS NOT NULL THEN datetime(ReconciledAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // CashCollections
            migrationBuilder.Sql(@"UPDATE CashCollections SET 
                CollectionDate = datetime(CollectionDate, '-6 hours'),
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // CashCutReconciliations
            migrationBuilder.Sql(@"UPDATE CashCutReconciliations SET 
                ReconciliationDate = datetime(ReconciliationDate, '-6 hours'),
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // Returns
            migrationBuilder.Sql(@"UPDATE Returns SET 
                ReturnDate = datetime(ReturnDate, '-6 hours'),
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // Cancellations
            migrationBuilder.Sql(@"UPDATE Cancellations SET 
                CancellationDate = datetime(CancellationDate, '-6 hours'),
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // InventoryMovements
            migrationBuilder.Sql(@"UPDATE InventoryMovements SET 
                Date = datetime(Date, '-6 hours'),
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // InventoryDocuments
            migrationBuilder.Sql(@"UPDATE InventoryDocuments SET 
                Date = datetime(Date, '-6 hours'),
                LastAttemptAt = CASE WHEN LastAttemptAt IS NOT NULL THEN datetime(LastAttemptAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // DeliveryRoutes
            migrationBuilder.Sql(@"UPDATE DeliveryRoutes SET 
                CreatedDate = datetime(CreatedDate, '-6 hours'),
                DispatchedDate = CASE WHEN DispatchedDate IS NOT NULL THEN datetime(DispatchedDate, '-6 hours') ELSE NULL END,
                SettledDate = CASE WHEN SettledDate IS NOT NULL THEN datetime(SettledDate, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // UserWorkStatuses
            migrationBuilder.Sql(@"UPDATE UserWorkStatuses SET 
                LastStatusChangeAt = datetime(LastStatusChangeAt, '-6 hours'),
                LastAssignedOrderAt = CASE WHEN LastAssignedOrderAt IS NOT NULL THEN datetime(LastAssignedOrderAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // ContpaqiSyncQueues
            migrationBuilder.Sql(@"UPDATE ContpaqiSyncQueues SET 
                EnqueuedAt = datetime(EnqueuedAt, '-6 hours'),
                LastAttemptAt = CASE WHEN LastAttemptAt IS NOT NULL THEN datetime(LastAttemptAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // InboxMessages
            migrationBuilder.Sql(@"UPDATE InboxMessages SET 
                ReceivedAt = datetime(ReceivedAt, '-6 hours'),
                ProcessedAt = CASE WHEN ProcessedAt IS NOT NULL THEN datetime(ProcessedAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // OutboxMessages
            migrationBuilder.Sql(@"UPDATE OutboxMessages SET 
                EnqueuedAt = datetime(EnqueuedAt, '-6 hours'),
                LastAttemptAt = CASE WHEN LastAttemptAt IS NOT NULL THEN datetime(LastAttemptAt, '-6 hours') ELSE NULL END,
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");

            // AuditLogs
            migrationBuilder.Sql(@"UPDATE AuditLogs SET 
                Timestamp = datetime(Timestamp, '-6 hours'),
                CreatedAt = datetime(CreatedAt, '-6 hours'),
                LastModifiedAt = CASE WHEN LastModifiedAt IS NOT NULL THEN datetime(LastModifiedAt, '-6 hours') ELSE NULL END,
                DeletedAt = CASE WHEN DeletedAt IS NOT NULL THEN datetime(DeletedAt, '-6 hours') ELSE NULL END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert +6 hours if rolled back
            migrationBuilder.Sql(@"UPDATE Sales SET Date = datetime(Date, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE Shifts SET StartTime = datetime(StartTime, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE Orders SET OrderDate = datetime(OrderDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE Invoices SET InvoiceDate = datetime(InvoiceDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE CashCuts SET CutDate = datetime(CutDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE CashCollections SET CollectionDate = datetime(CollectionDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE CashCutReconciliations SET ReconciliationDate = datetime(ReconciliationDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE Returns SET ReturnDate = datetime(ReturnDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE Cancellations SET CancellationDate = datetime(CancellationDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE InventoryMovements SET Date = datetime(Date, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE InventoryDocuments SET Date = datetime(Date, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE DeliveryRoutes SET CreatedDate = datetime(CreatedDate, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE UserWorkStatuses SET LastStatusChangeAt = datetime(LastStatusChangeAt, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
            migrationBuilder.Sql(@"UPDATE AuditLogs SET Timestamp = datetime(Timestamp, '+6 hours'), CreatedAt = datetime(CreatedAt, '+6 hours');");
        }
    }
}
