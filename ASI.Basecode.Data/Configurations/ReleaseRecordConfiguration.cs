using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
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
}

