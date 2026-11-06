using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
    public class AdministrationAuditEventConfiguration : IEntityTypeConfiguration<AdministrationAuditEvent>
    {
        public void Configure(EntityTypeBuilder<AdministrationAuditEvent> builder)
        {
            builder.ToTable("AdministrationAuditEvent");
            builder.HasKey(x => x.AdministrationAuditEventId);
            builder.Property(x => x.AdministrationAuditEventId).UseIdentityColumn();
            builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
            builder.Property(x => x.TargetUserId).HasMaxLength(450);
            builder.Property(x => x.TargetRoleId).HasMaxLength(450);
            builder.Property(x => x.Action).HasMaxLength(80).IsRequired();
            builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.OccurredAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => x.TargetUserId);
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.TargetUserId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Microsoft.AspNetCore.Identity.IdentityRole>()
                .WithMany()
                .HasForeignKey(x => x.TargetRoleId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

