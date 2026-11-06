using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");
            builder.HasKey(x => x.NotificationId);
            builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Body).IsRequired().HasMaxLength(2000);
            builder.Property(x => x.LinkUrl).HasMaxLength(500);
            builder.Property(x => x.DedupKey).IsRequired().HasMaxLength(64);
            builder.Property(x => x.EmailStatus).HasMaxLength(20);
            builder.Property(x => x.EmailLastError).HasMaxLength(500);
            builder.Property(x => x.EmailLeaseToken).HasMaxLength(32);
            builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => x.DedupKey).IsUnique();
            builder.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
            builder.HasIndex(x => new { x.EmailStatus, x.EmailNextAttemptAt }).HasFilter("[EmailRequested] = 1");
        }
    }
}
