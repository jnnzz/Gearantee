using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data
{
    public class AsiBasecodeDBContext
        : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public AsiBasecodeDBContext(DbContextOptions<AsiBasecodeDBContext> options)
            : base(options)
        {
        }

        protected AsiBasecodeDBContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<RolePermissionSeed> RolePermissionSeeds { get; set; }
        public DbSet<AdministrationAuditEvent> AdministrationAuditEvents { get; set; }
        public DbSet<BorrowerProfile> BorrowerProfiles { get; set; }
        public DbSet<EquipmentCategory> EquipmentCategories { get; set; }
        public DbSet<EquipmentItem> EquipmentItems { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReleaseRecord> ReleaseRecords { get; set; }
        public DbSet<ReturnRecord> ReturnRecords { get; set; }
        public DbSet<LateReturn> LateReturns { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // External login providers are not used, but user/role claims are read by the
            // Identity claims factory on every sign-in and must stay mapped.
            modelBuilder.Ignore<IdentityUserLogin<string>>();
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsiBasecodeDBContext).Assembly);
        }
    }
}
