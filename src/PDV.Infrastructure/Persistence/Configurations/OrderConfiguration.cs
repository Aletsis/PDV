using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.TotalTax).HasPrecision(18, 2);
        entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        
        entity.Property(e => e.Channel).HasDefaultValue(PDV.Domain.Enums.OrderChannel.Telephone);
        entity.Property(e => e.Series).HasMaxLength(10);
        entity.Property(e => e.ReturnReason).HasMaxLength(250);
        entity.Property(e => e.CancellationReason).HasMaxLength(250);
        entity.Property(e => e.GeneralNotes).HasMaxLength(500);
        entity.Property(e => e.DeliveryNotes).HasMaxLength(500);
        entity.Property(e => e.DeliveryManId).HasMaxLength(50);
        entity.Property(e => e.TakenById).HasMaxLength(50);
        entity.Property(e => e.FilledById).HasMaxLength(50);
        entity.Property(e => e.CapturedById).HasMaxLength(50);
        entity.Property(e => e.VerifiedById).HasMaxLength(50);
        entity.Property(e => e.RoutedById).HasMaxLength(50);
        entity.Property(e => e.SettledById).HasMaxLength(50);
        entity.Property(e => e.AuthorizedBySupervisorId).HasMaxLength(50);

        entity.OwnsMany(e => e.Taxes, a =>
        {
            a.ToTable("OrderTaxBreakdowns");
            a.WithOwner().HasForeignKey("OrderId");
            a.HasKey("Id");
            a.Property("Id").ValueGeneratedOnAdd();
            a.Property(x => x.BaseAmount).HasPrecision(18, 2);
            a.Property(x => x.TaxAmount).HasPrecision(18, 2);
            a.Property(x => x.Rate).HasPrecision(18, 4);
        });

        entity.HasOne(e => e.Branch)
              .WithMany()
              .HasForeignKey(e => e.BranchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Client)
              .WithMany()
              .HasForeignKey(e => e.ClientId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.CashRegister)
              .WithMany()
              .HasForeignKey(e => e.CashRegisterId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.DeliveryRoute)
              .WithMany(r => r.Orders)
              .HasForeignKey(e => e.DeliveryRouteId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.DeliveryZone)
              .WithMany()
              .HasForeignKey(e => e.DeliveryZoneId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Shift)
              .WithMany()
              .HasForeignKey(e => e.ShiftId)
              .OnDelete(DeleteBehavior.SetNull);


        entity.HasIndex(e => new { e.BranchId, e.OrderDate })
              .HasDatabaseName("IX_Orders_BranchId_OrderDate");
    }
}
