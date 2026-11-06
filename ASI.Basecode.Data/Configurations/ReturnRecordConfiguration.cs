using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
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
}

