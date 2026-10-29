using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
    public class EquipmentItemConfiguration : IEntityTypeConfiguration<EquipmentItem>
    {
        public void Configure(EntityTypeBuilder<EquipmentItem> builder)
        {
            builder.ToTable("EquipmentItem");
            builder.HasKey(x => x.EquipmentId);
            builder.Property(x => x.EquipmentId).UseIdentityColumn();
            builder.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Location).HasColumnType("varchar(200)").IsRequired();
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
        }
    }
}

