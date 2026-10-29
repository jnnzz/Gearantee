using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.Data.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Data
{
    public class DatabaseSeederTests : IDisposable
    {
        private readonly SqliteDb _sqlite;

        public DatabaseSeederTests()
        {
            _sqlite = new SqliteDb();
        }

        public void Dispose()
        {
            _sqlite.Dispose();
        }

        private ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddDebug());
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<SqliteDashboardDbContext>(options =>
                options.UseSqlite(_sqlite.Connection));
            services.AddScoped<AsiBasecodeDBContext>(provider =>
                provider.GetRequiredService<SqliteDashboardDbContext>());

            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SqliteDashboardDbContext>()
                .AddDefaultTokenProviders();

            return services.BuildServiceProvider();
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        [Fact]
        public async Task SeedAsync_MissingAdminConfiguration_CreatesNoAdministratorAccount()
        {
            using var provider = CreateProvider();
            var config = CreateConfiguration(new Dictionary<string, string>());

            await DatabaseSeeder.SeedAsync(provider, config);

            using var scope = provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var users = await userManager.Users.ToListAsync();
            Assert.Empty(users);
        }

        [Fact]
        public async Task SeedAsync_ReseedingDoesNotRestoreRemovedRolePermissions()
        {
            using var provider = CreateProvider();
            var config = CreateConfiguration(new Dictionary<string, string>());

            await DatabaseSeeder.SeedAsync(provider, config);

            string custodianRoleId;
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var custodian = await db.Roles
                    .SingleAsync(role => role.Name == DomainValues.Roles.Custodian);
                custodianRoleId = custodian.Id;

                var grants = await db.RolePermissions
                    .Where(grant => grant.RoleId == custodianRoleId)
                    .ToListAsync();
                Assert.NotEmpty(grants);
                db.RolePermissions.RemoveRange(grants);
                await db.SaveChangesAsync();
            }

            // Re-running the seeder must treat the (now empty) role as already initialized.
            await DatabaseSeeder.SeedAsync(provider, config);

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                Assert.Empty(await db.RolePermissions
                    .Where(grant => grant.RoleId == custodianRoleId)
                    .ToListAsync());
            }
        }

        [Theory]
        [InlineData(null, "Custodian!123456", "CUST-001")]
        [InlineData("custodian@test.local", "   ", "CUST-001")]
        [InlineData("custodian@test.local", "Custodian!123456", null)]
        public async Task SeedAsync_IncompleteCustodianConfiguration_CreatesNoAccount(
            string email, string password, string userCode)
        {
            using var provider = CreateProvider();
            await DatabaseSeeder.SeedAsync(provider, CreateConfiguration(new Dictionary<string, string>
            {
                ["SeedCustodian:Email"] = email,
                ["SeedCustodian:Password"] = password,
                ["SeedCustodian:UserCode"] = userCode
            }));
            using var scope = provider.CreateScope();
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().Users.ToListAsync());
        }

        [Fact]
        public async Task SeedAsync_CustodianHasOnlyCustodianRole_AndReseedingPreservesAccountChanges()
        {
            using var provider = CreateProvider();
            var settings = new Dictionary<string, string>
            {
                ["SeedCustodian:Email"] = "custodian@test.local",
                ["SeedCustodian:Password"] = "Custodian!123456",
                ["SeedCustodian:UserCode"] = "CUST-001"
            };
            await DatabaseSeeder.SeedAsync(provider, CreateConfiguration(settings));
            await DatabaseSeeder.SeedAsync(provider, CreateConfiguration(settings));
            using (var scope = provider.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = Assert.Single(await users.Users.ToListAsync());
                Assert.Equal("CUST-001", user.UserCode);
                Assert.True(user.IsActive);
                Assert.True(user.EmailConfirmed);
                Assert.Equal(DomainValues.Roles.Custodian, Assert.Single(await users.GetRolesAsync(user)));
                Assert.True(await users.CheckPasswordAsync(user, "Custodian!123456"));
                Assert.True((await users.RemoveFromRoleAsync(user, DomainValues.Roles.Custodian)).Succeeded);
                user.IsActive = false;
                Assert.True((await users.UpdateAsync(user)).Succeeded);
            }
            settings["SeedCustodian:Password"] = "Changed!123456";
            await DatabaseSeeder.SeedAsync(provider, CreateConfiguration(settings));
            using (var scope = provider.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = Assert.Single(await users.Users.ToListAsync());
                Assert.False(user.IsActive);
                Assert.Empty(await users.GetRolesAsync(user));
                Assert.True(await users.CheckPasswordAsync(user, "Custodian!123456"));
                Assert.False(await users.CheckPasswordAsync(user, "Changed!123456"));
            }
        }

        [Theory]
        [InlineData("custodian@test.local", "EXISTING-001")]
        [InlineData("existing@test.local", "CUST-001")]
        public async Task SeedAsync_CustodianConfigurationDoesNotPromoteExistingAccount(string email, string code)
        {
            using var provider = CreateProvider();
            using (var scope = provider.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                Assert.True((await users.CreateAsync(new ApplicationUser
                {
                    UserName = code, UserCode = code, Email = email,
                    FirstName = "Existing", LastName = "User", IsActive = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }, "Original!123456")).Succeeded);
            }
            await DatabaseSeeder.SeedAsync(provider, CreateConfiguration(new Dictionary<string, string>
            {
                ["SeedCustodian:Email"] = "custodian@test.local",
                ["SeedCustodian:Password"] = "Custodian!123456",
                ["SeedCustodian:UserCode"] = "CUST-001"
            }));
            using (var scope = provider.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = Assert.Single(await users.Users.ToListAsync());
                Assert.Empty(await users.GetRolesAsync(user));
                Assert.True(await users.CheckPasswordAsync(user, "Original!123456"));
            }
        }

        [Theory]
        [InlineData("admin@test.local", null, "ADM-001")]
        [InlineData("admin@test.local", "", "ADM-001")]
        [InlineData("admin@test.local", "Admin!123456", null)]
        [InlineData("admin@test.local", "Admin!123456", "   ")]
        [InlineData(null, "Admin!123456", "ADM-001")]
        [InlineData("   ", "Admin!123456", "ADM-001")]
        public async Task SeedAsync_PartialOrWhitespaceAdminConfiguration_CreatesNoAdministratorAccount(
            string email, string password, string userCode)
        {
            using var provider = CreateProvider();
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["SeedAdmin:Email"] = email,
                ["SeedAdmin:Password"] = password,
                ["SeedAdmin:UserCode"] = userCode
            });

            await DatabaseSeeder.SeedAsync(provider, config);

            using var scope = provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var users = await userManager.Users.ToListAsync();
            Assert.Empty(users);
        }

        [Fact]
        public async Task SeedAsync_ExplicitValidConfiguration_CreatesExpectedAdministrator()
        {
            using var provider = CreateProvider();
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["SeedAdmin:Email"] = "explicit.admin@gearantee.local",
                ["SeedAdmin:Password"] = "Explicit!123456",
                ["SeedAdmin:UserCode"] = "ADM-EXPL-001",
                ["SeedAdmin:FirstName"] = "Custom",
                ["SeedAdmin:LastName"] = "Admin"
            });

            await DatabaseSeeder.SeedAsync(provider, config);

            using var scope = provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("explicit.admin@gearantee.local");

            Assert.NotNull(user);
            Assert.Equal("ADM-EXPL-001", user.UserCode);
            Assert.Equal("Custom", user.FirstName);
            Assert.Equal("Admin", user.LastName);
            Assert.True(user.IsActive);
            Assert.True(await userManager.IsInRoleAsync(user, DomainValues.Roles.Administrator));
            Assert.True(await userManager.CheckPasswordAsync(user, "Explicit!123456"));
            Assert.False(await userManager.CheckPasswordAsync(user, "ChangeMe!123"));
        }

        [Fact]
        public async Task SeedAsync_ReseedingDoesNotRepromoteExistingUserOrRotatePassword()
        {
            using var provider = CreateProvider();

            // First create an existing non-admin user
            using (var initScope = provider.CreateScope())
            {
                var userManager = initScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var existingUser = new ApplicationUser
                {
                    UserName = "EXISTING-001",
                    UserCode = "EXISTING-001",
                    Email = "existing@gearantee.local",
                    EmailConfirmed = true,
                    FirstName = "Existing",
                    LastName = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var createResult = await userManager.CreateAsync(existingUser, "OriginalPass!123");
                Assert.True(createResult.Succeeded);
            }

            // Attempt to seed administrator with that same email
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["SeedAdmin:Email"] = "existing@gearantee.local",
                ["SeedAdmin:Password"] = "AttemptedNewPass!456",
                ["SeedAdmin:UserCode"] = "EXISTING-001"
            });

            await DatabaseSeeder.SeedAsync(provider, config);

            // Verify user was NOT promoted to Administrator and password was NOT rotated
            using (var verifyScope = provider.CreateScope())
            {
                var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByEmailAsync("existing@gearantee.local");

                Assert.NotNull(user);
                Assert.False(await userManager.IsInRoleAsync(user, DomainValues.Roles.Administrator));
                Assert.True(await userManager.CheckPasswordAsync(user, "OriginalPass!123"));
                Assert.False(await userManager.CheckPasswordAsync(user, "AttemptedNewPass!456"));
            }
        }
    }
}
