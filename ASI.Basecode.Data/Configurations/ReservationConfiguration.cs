using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
    public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.ToTable("Reservation", table =>
                table.HasCheckConstraint(
                    "CK_Reservation_DateRange",
                    "[ReservationStart] < [ReservationEnd]"));
            builder.HasKey(x => x.ReservationId);
            builder.Property(x => x.ReservationId).UseIdentityColumn();
            builder.Property(x => x.ReviewedByUserId).HasMaxLength(450);
            builder.Property(x => x.RequestedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.Property(x => x.ReservationStart).HasColumnType("datetime2");
            builder.Property(x => x.ReservationEnd).HasColumnType("datetime2");
            builder.Property(x => x.Purpose).HasMaxLength(1000).IsRequired();
            builder.Property(x => x.Status)
                .HasMaxLength(50)
                .HasDefaultValue(DomainValues.ReservationStatuses.Pending)
                .IsRequired();
            builder.Property(x => x.RejectionReason).HasMaxLength(1000);
            builder.Property(x => x.ReviewedAt).HasColumnType("datetime2");
            builder.Property(x => x.CancelledAt).HasColumnType("datetime2");
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.Property(x => x.UpdatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.HasIndex(x => new { x.EquipmentId, x.ReservationStart, x.ReservationEnd });
            builder.HasIndex(x => new
            {
                x.BorrowerProfileId,
                x.EquipmentId,
                x.Status,
                x.ReservationStart,
                x.ReservationEnd
            });
            builder.HasIndex(x => x.Status);
            builder.HasOne(x => x.BorrowerProfile)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.BorrowerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.EquipmentItem)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

