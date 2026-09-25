using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.Property(x => x.UserCode).HasMaxLength(100).IsRequired();
            builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.Property(x => x.UpdatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.HasIndex(x => x.UserCode).IsUnique();
        }
    }

    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("Permission");
            builder.HasKey(x => x.PermissionId);
            builder.Property(x => x.PermissionId).UseIdentityColumn();
            builder.Property(x => x.PermissionName).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.HasIndex(x => x.PermissionName).IsUnique();
        }
    }

    public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("RolePermission");
            builder.HasKey(x => new { x.RoleId, x.PermissionId });
            builder.Property(x => x.RoleId).HasMaxLength(450);
            builder.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class BorrowerProfileConfiguration : IEntityTypeConfiguration<BorrowerProfile>
    {
        public void Configure(EntityTypeBuilder<BorrowerProfile> builder)
        {
            builder.ToTable("BorrowerProfile");
            builder.HasKey(x => x.BorrowerProfileId);
            builder.Property(x => x.BorrowerProfileId).UseIdentityColumn();
            builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            builder.Property(x => x.SchoolId).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Department).HasMaxLength(200).IsRequired();
            builder.Property(x => x.ContactNumber).HasMaxLength(50);
            builder.Property(x => x.IsEligible).HasDefaultValue(false);
            ConfigureAuditDates(builder);

            builder.HasIndex(x => x.UserId).IsUnique();
            builder.HasIndex(x => x.SchoolId).IsUnique();
            builder.HasOne(x => x.User)
                .WithOne(x => x.BorrowerProfile)
                .HasForeignKey<BorrowerProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private static void ConfigureAuditDates(EntityTypeBuilder<BorrowerProfile> builder)
        {
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.Property(x => x.UpdatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
        }
    }

    public class EquipmentCategoryConfiguration : IEntityTypeConfiguration<EquipmentCategory>
    {
        public void Configure(EntityTypeBuilder<EquipmentCategory> builder)
        {
            builder.ToTable("EquipmentCategory");
            builder.HasKey(x => x.CategoryId);
            builder.Property(x => x.CategoryId).UseIdentityColumn();
            builder.Property(x => x.CategoryCode).HasMaxLength(50).IsRequired();
            builder.Property(x => x.CategoryName).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.Property(x => x.UpdatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.HasIndex(x => x.CategoryCode).IsUnique();
            builder.HasIndex(x => x.CategoryName).IsUnique();
        }
    }

    public class LocationConfiguration : IEntityTypeConfiguration<Location>
    {
        public void Configure(EntityTypeBuilder<Location> builder)
        {
            builder.ToTable("Location");
            builder.HasKey(x => x.LocationId);
            builder.Property(x => x.LocationId).UseIdentityColumn();
            builder.Property(x => x.LocationName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.HasIndex(x => x.LocationName).IsUnique();
        }
    }

    public class EquipmentItemConfiguration : IEntityTypeConfiguration<EquipmentItem>
    {
        public void Configure(EntityTypeBuilder<EquipmentItem> builder)
        {
            builder.ToTable("EquipmentItem");
            builder.HasKey(x => x.EquipmentId);
            builder.Property(x => x.EquipmentId).UseIdentityColumn();
            builder.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Brand).HasMaxLength(150);
            builder.Property(x => x.Model).HasMaxLength(150);
            builder.Property(x => x.SerialNumber).HasMaxLength(200);
            builder.Property(x => x.ConditionStatus).HasMaxLength(50).IsRequired();
            builder.Property(x => x.ItemStatus)
                .HasMaxLength(50)
                .HasDefaultValue(DomainValues.EquipmentStatuses.Available)
                .IsRequired();
            builder.Property(x => x.ImageUrl).HasMaxLength(500);
            builder.Property(x => x.IsArchived).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.Property(x => x.UpdatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.HasIndex(x => x.ItemCode).IsUnique();
            builder.HasIndex(x => x.SerialNumber)
                .IsUnique()
                .HasFilter("[SerialNumber] IS NOT NULL");
            builder.HasOne(x => x.Category)
                .WithMany(x => x.EquipmentItems)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Location)
                .WithMany(x => x.EquipmentItems)
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

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

    public class ReleaseRecordConfiguration : IEntityTypeConfiguration<ReleaseRecord>
    {
        public void Configure(EntityTypeBuilder<ReleaseRecord> builder)
        {
            builder.ToTable("ReleaseRecord");
            builder.HasKey(x => x.ReleaseRecordId);
            builder.Property(x => x.ReleaseRecordId).UseIdentityColumn();
            builder.Property(x => x.ReleasedByUserId).HasMaxLength(450).IsRequired();
            builder.Property(x => x.ActualReleaseAt).HasColumnType("datetime2");
            builder.Property(x => x.Notes).HasMaxLength(1000);
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.HasIndex(x => x.ReservationId).IsUnique();
            builder.HasOne(x => x.Reservation)
                .WithOne(x => x.ReleaseRecord)
                .HasForeignKey<ReleaseRecord>(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ReleasedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReleasedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class ReturnRecordConfiguration : IEntityTypeConfiguration<ReturnRecord>
    {
        public void Configure(EntityTypeBuilder<ReturnRecord> builder)
        {
            builder.ToTable("ReturnRecord");
            builder.HasKey(x => x.ReturnRecordId);
            builder.Property(x => x.ReturnRecordId).UseIdentityColumn();
            builder.Property(x => x.ReceivedByUserId).HasMaxLength(450).IsRequired();
            builder.Property(x => x.ActualReturnAt).HasColumnType("datetime2");
            builder.Property(x => x.ReturnedCondition).HasMaxLength(50).IsRequired();
            builder.Property(x => x.ResultingItemStatus).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Notes).HasMaxLength(1000);
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.HasIndex(x => x.ReleaseRecordId).IsUnique();
            builder.HasOne(x => x.ReleaseRecord)
                .WithOne(x => x.ReturnRecord)
                .HasForeignKey<ReturnRecord>(x => x.ReleaseRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ReceivedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReceivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class LateReturnConfiguration : IEntityTypeConfiguration<LateReturn>
    {
        public void Configure(EntityTypeBuilder<LateReturn> builder)
        {
            builder.ToTable("LateReturn", table =>
                table.HasCheckConstraint(
                    "CK_LateReturn_Dates",
                    "[ReturnedAt] > [DueAt] AND [DaysLate] >= 0"));
            builder.HasKey(x => x.LateReturnId);
            builder.Property(x => x.LateReturnId).UseIdentityColumn();
            builder.Property(x => x.DueAt).HasColumnType("datetime2");
            builder.Property(x => x.ReturnedAt).HasColumnType("datetime2");
            builder.Property(x => x.PenaltyStatus).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Notes).HasMaxLength(1000);
            builder.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.HasIndex(x => x.ReturnRecordId).IsUnique();
            builder.HasOne(x => x.ReturnRecord)
                .WithOne(x => x.LateReturn)
                .HasForeignKey<LateReturn>(x => x.ReturnRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
