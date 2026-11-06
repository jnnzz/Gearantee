using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASI.Basecode.Data.Configurations
{
    public class RolePermissionSeedConfiguration : IEntityTypeConfiguration<RolePermissionSeed>
    {
        public void Configure(EntityTypeBuilder<RolePermissionSeed> builder)
        {
            builder.ToTable("RolePermissionSeed");
            builder.HasKey(x => x.RoleId);
            builder.Property(x => x.RoleId).HasMaxLength(450);
            builder.HasOne<IdentityRole>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
