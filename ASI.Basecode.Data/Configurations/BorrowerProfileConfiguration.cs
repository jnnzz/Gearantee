using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
    public class BorrowerProfileConfiguration : IEntityTypeConfiguration<BorrowerProfile>
    {
        public void Configure(EntityTypeBuilder<BorrowerProfile> builder)
        {
            builder.ToTable("BorrowerProfile");
            builder.HasKey(x => x.BorrowerProfileId);
            builder.Property(x => x.BorrowerProfileId).UseIdentityColumn();
            builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            builder.Property(x => x.Department).HasMaxLength(200).IsRequired();
            builder.Property(x => x.ContactNumber).HasMaxLength(50);
            builder.Property(x => x.IsEligible).HasDefaultValue(false);
            builder.Property(x => x.ConcurrencyStamp)
                .HasMaxLength(64)
                .IsRequired()
                .IsConcurrencyToken();
            ConfigureAuditDates(builder);

            builder.HasIndex(x => x.UserId).IsUnique();
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
}

