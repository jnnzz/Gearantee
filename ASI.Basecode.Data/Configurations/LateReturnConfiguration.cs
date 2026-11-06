using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
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

