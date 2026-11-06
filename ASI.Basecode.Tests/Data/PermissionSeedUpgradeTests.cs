using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Seeding;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Tests.UserAdministration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Data
{
    /// <summary>
    /// Upgrade regression: the schema-normalization migration must not discard the
    /// RolePermissionSeed marker, including for roles that were deliberately left with zero
    /// grants. Otherwise a later --seed run silently restores removed privileges.
    /// </summary>
    public sealed class PermissionSeedUpgradeTests
    {
        [SqlServerFact]
        public async Task UpgradingPreservesMarkerForEmptyRoleAndReseedingKeepsItEmpty()
        {
            var connectionString =
                @"Server=(localdb)\MSSQLLocalDB;Database=Gearantee_Upgrade_Test_" +
                Guid.NewGuid().ToString("N") +
                ";Trusted_Connection=True;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using var db = new AsiBasecodeDBContext(options);
            try
            {
                var migrator = db.GetService<IMigrator>();

                // Start at the schema just before the normalization migration.
                await migrator.MigrateAsync(
                    "20261016042221_AddBorrowerProfileConcurrencyStamp");

                var custodian = new IdentityRole(DomainValues.Roles.Custodian);
                db.Roles.Add(custodian);
                await db.SaveChangesAsync();
                db.RolePermissionSeeds.Add(new RolePermissionSeed { RoleId = custodian.Id });
                await db.SaveChangesAsync();
                // The Custodian role is intentionally left with no grants but a marker.

                // Apply the normalization migration (the upgrade under test).
                await migrator.MigrateAsync();

                db.ChangeTracker.Clear();
                Assert.True(await db.Roles.AnyAsync(role => role.Id == custodian.Id));
                Assert.True(await db.RolePermissionSeeds
                    .AnyAsync(seed => seed.RoleId == custodian.Id));
                Assert.False(await db.RolePermissions
                    .AnyAsync(grant => grant.RoleId == custodian.Id));

                // Reseeding must honor the surviving marker and keep the role empty.
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddDbContext<AsiBasecodeDBContext>(builder =>
                    builder.UseSqlServer(connectionString));
                services.AddIdentityCore<ApplicationUser>()
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<AsiBasecodeDBContext>()
                    .AddDefaultTokenProviders();
                await using var provider = services.BuildServiceProvider();

                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>())
                    .Build();
                await DatabaseSeeder.SeedAsync(provider, configuration);

                using var verify = new AsiBasecodeDBContext(
                    new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                        .UseSqlServer(connectionString)
                        .Options);
                Assert.True(await verify.RolePermissionSeeds
                    .AnyAsync(seed => seed.RoleId == custodian.Id));
                Assert.False(await verify.RolePermissions
                    .AnyAsync(grant => grant.RoleId == custodian.Id));
            }
            finally
            {
                await db.Database.EnsureDeletedAsync();
            }
        }
    }
}