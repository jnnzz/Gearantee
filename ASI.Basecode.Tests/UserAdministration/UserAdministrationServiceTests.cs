using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.WebApp.Data;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.UserAdministration;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.UserAdministration
{
    public class UserAdministrationServiceTests
    {
        [Fact]
        public async Task CanManageAsync_RequiresCurrentActiveAdminPermission()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminRole = await AddRoleAsync(roles, DomainValues.Roles.Administrator);
            var admin = await AddUserAsync(users, "ADMIN-01", "active-admin@test.local");
            Assert.True((await users.AddToRoleAsync(admin, adminRole.Name)).Succeeded);
            var permission = AddPermission(db, DomainValues.Permissions.UserRoleManage);
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permission.PermissionId
            });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            Assert.True(await service.CanManageAsync(admin.Id));

            db.RolePermissions.RemoveRange(db.RolePermissions);
            await db.SaveChangesAsync();
            Assert.False(await service.CanManageAsync(admin.Id));

            admin.IsActive = false;
            await users.UpdateAsync(admin);
            Assert.False(await service.CanManageAsync(admin.Id));
        }

        [Fact]
        public async Task CreateAsync_CreatesIdentityBorrowerAndIneligibleProfile_WithoutAuditingPassword()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminRole = await AddRoleAsync(roles, DomainValues.Roles.Administrator);
            var borrowerRole = await AddRoleAsync(roles, DomainValues.Roles.Borrower);
            var actor = await AddUserAsync(users, "ADMIN-02", "creator@test.local");
            Assert.True((await users.AddToRoleAsync(actor, adminRole.Name)).Succeeded);
            var permission = AddPermission(db, DomainValues.Permissions.UserRoleManage);
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permission.PermissionId
            });
            await db.SaveChangesAsync();

            const string password = "S3cret!OnlyForTheUser";
            var result = await scope.ServiceProvider
                .GetRequiredService<IUserAdministrationService>()
                .CreateAsync(actor.Id, new CreateUserViewModel
                {
                    UserCode = "STU-2001",
                    FirstName = "Mara",
                    LastName = "Santos",
                    Email = "mara@test.local",
                    RoleName = borrowerRole.Name,
                    SchoolId = "STU-2001",
                    Department = "Information Technology",
                    Password = password,
                    ConfirmPassword = password
                });

            Assert.True(result.Succeeded, result.Message);
            var created = await users.FindByEmailAsync("mara@test.local");
            Assert.NotNull(created);
            Assert.True(await users.IsInRoleAsync(created, DomainValues.Roles.Borrower));
            var profile = await db.BorrowerProfiles.SingleAsync(item => item.UserId == created.Id);
            Assert.False(profile.IsEligible);
            Assert.DoesNotContain(password,
                await db.AdministrationAuditEvents.Select(item => item.DetailsJson).SingleAsync());
            Assert.Equal(actor.Id,
                await db.AdministrationAuditEvents.Select(item => item.ActorUserId).SingleAsync());
        }

        [Fact]
        public async Task UpdatePermissionsAsync_PersistsMatrixAndRejectsStaleRoleStamps()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminRole = await AddRoleAsync(roles, DomainValues.Roles.Administrator);
            var borrowerRole = await AddRoleAsync(roles, DomainValues.Roles.Borrower);
            var custodianRole = await AddRoleAsync(roles, DomainValues.Roles.Custodian);
            var actor = await AddUserAsync(users, "ADMIN-03", "permission-admin@test.local");
            Assert.True((await users.AddToRoleAsync(actor, adminRole.Name)).Succeeded);

            var permissions = new[]
            {
                DomainValues.Permissions.EquipmentBrowse,
                DomainValues.Permissions.ReservationCreate,
                DomainValues.Permissions.ReservationReview,
                DomainValues.Permissions.TransactionReleaseReturn,
                DomainValues.Permissions.EquipmentManage,
                DomainValues.Permissions.BorrowerManage,
                DomainValues.Permissions.UserRoleManage,
                DomainValues.Permissions.HistoryView
            };
            foreach (var permissionName in permissions)
            {
                AddPermission(db, permissionName);
            }
            db.RolePermissions.AddRange(
                db.Permissions.Select(permission => new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.PermissionId
                }));
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = borrowerRole.Id,
                PermissionId = db.Permissions.Single(item =>
                    item.PermissionName == DomainValues.Permissions.EquipmentBrowse).PermissionId
            });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            var matrix = await service.GetRolePermissionsAsync(actor.Id);
            var submission = new UpdateRolePermissionsViewModel
            {
                RoleStamps = matrix.RoleStamps.ToDictionary(item => item.Key, item => item.Value),
                Assignments = matrix.Permissions.Select(permission => new PermissionSelectionViewModel
                {
                    PermissionName = permission.Name,
                    RoleIds = permission.GrantedRoleIds
                        .Where(id => !(id == borrowerRole.Id &&
                            permission.Name == DomainValues.Permissions.EquipmentBrowse))
                        .ToList()
                }).ToList()
            };

            var result = await service.UpdatePermissionsAsync(actor.Id, submission);
            Assert.True(result.Succeeded, result.Message);
            Assert.False(await db.RolePermissions.AnyAsync(link =>
                link.RoleId == borrowerRole.Id &&
                link.PermissionId == db.Permissions.Single(item =>
                    item.PermissionName == DomainValues.Permissions.EquipmentBrowse).PermissionId));
            Assert.True(await db.RolePermissions.AnyAsync(link =>
                link.RoleId == adminRole.Id &&
                link.PermissionId == db.Permissions.Single(item =>
                    item.PermissionName == DomainValues.Permissions.UserRoleManage).PermissionId));
            Assert.Empty(await db.AdministrationAuditEvents.Where(item =>
                item.TargetRoleId == custodianRole.Id).ToListAsync());

            var stale = await service.UpdatePermissionsAsync(actor.Id, submission);
            Assert.True(stale.Conflict);
        }

        [Fact]
        public async Task SetActiveAsync_CannotDeactivateActor_AndPreventsTwoAdminsDisablingEachOther()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminRole = await AddRoleAsync(roles, DomainValues.Roles.Administrator);
            var first = await AddUserAsync(users, "ADMIN-04", "admin-one@test.local");
            var second = await AddUserAsync(users, "ADMIN-05", "admin-two@test.local");
            Assert.True((await users.AddToRoleAsync(first, adminRole.Name)).Succeeded);
            Assert.True((await users.AddToRoleAsync(second, adminRole.Name)).Succeeded);
            var permission = AddPermission(db, DomainValues.Permissions.UserRoleManage);
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permission.PermissionId
            });
            await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();

            var selfAttempt = await service.SetActiveAsync(first.Id,
                new SetAccountActiveViewModel
                {
                    UserId = first.Id,
                    ConcurrencyStamp = first.ConcurrencyStamp,
                    IsActive = false
                });
            Assert.False(selfAttempt.Succeeded);

            var firstAttempt = await service.SetActiveAsync(first.Id,
                new SetAccountActiveViewModel
                {
                    UserId = second.Id,
                    ConcurrencyStamp = second.ConcurrencyStamp,
                    IsActive = false
                });
            Assert.True(firstAttempt.Succeeded, firstAttempt.Message);

            var staleOtherDirection = await service.SetActiveAsync(second.Id,
                new SetAccountActiveViewModel
                {
                    UserId = first.Id,
                    ConcurrencyStamp = first.ConcurrencyStamp,
                    IsActive = false
                });
            Assert.True(staleOtherDirection.Forbidden);
            Assert.True((await users.FindByIdAsync(first.Id)).IsActive);
            Assert.False((await users.FindByIdAsync(second.Id)).IsActive);
        }

        [Fact]
        public async Task SeedAsync_DoesNotRestoreEditedPermissionsOrReassignRemovedBootstrapRole()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            var settings = new Dictionary<string, string>
            {
                ["SeedAdmin:Email"] = "bootstrap@test.local",
                ["SeedAdmin:UserCode"] = "BOOTSTRAP-01",
                ["SeedAdmin:Password"] = "S3cret!BootstrapPass"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            await DatabaseSeeder.SeedAsync(provider, configuration);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var borrower = await roles.FindByNameAsync(DomainValues.Roles.Borrower);
                var browse = await db.Permissions.SingleAsync(permission =>
                    permission.PermissionName == DomainValues.Permissions.EquipmentBrowse);
                db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(link =>
                    link.RoleId == borrower.Id && link.PermissionId == browse.PermissionId));
                var admin = await users.FindByEmailAsync("bootstrap@test.local");
                Assert.True((await users.RemoveFromRoleAsync(
                    admin, DomainValues.Roles.Administrator)).Succeeded);
                await db.SaveChangesAsync();
            }

            await DatabaseSeeder.SeedAsync(provider, configuration);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var borrower = await roles.FindByNameAsync(DomainValues.Roles.Borrower);
                var browse = await db.Permissions.SingleAsync(permission =>
                    permission.PermissionName == DomainValues.Permissions.EquipmentBrowse);
                var administrator = await roles.FindByNameAsync(DomainValues.Roles.Administrator);
                var reservationCreate = await db.Permissions.SingleAsync(permission =>
                    permission.PermissionName == DomainValues.Permissions.ReservationCreate);
                Assert.False(await db.RolePermissions.AnyAsync(link =>
                    link.RoleId == borrower.Id && link.PermissionId == browse.PermissionId));
                Assert.False(await db.RolePermissions.AnyAsync(link =>
                    link.RoleId == administrator.Id &&
                    link.PermissionId == reservationCreate.PermissionId));
                var admin = await users.FindByEmailAsync("bootstrap@test.local");
                Assert.False(await users.IsInRoleAsync(admin, DomainValues.Roles.Administrator));
            }
        }

        private static ServiceProvider CreateProvider(SqliteDb database)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddDbContext<SqliteDashboardDbContext>(options =>
                options.UseSqlite(database.Connection));
            services.AddScoped<AsiBasecodeDBContext>(provider =>
                provider.GetRequiredService<SqliteDashboardDbContext>());
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SqliteDashboardDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IUserAdministrationService, UserAdministrationService>();
            return services.BuildServiceProvider();
        }

        private static async Task<IdentityRole> AddRoleAsync(
            RoleManager<IdentityRole> roleManager,
            string name)
        {
            var role = new IdentityRole(name);
            var result = await roleManager.CreateAsync(role);
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));
            return role;
        }

        private static async Task<ApplicationUser> AddUserAsync(
            UserManager<ApplicationUser> userManager,
            string code,
            string email)
        {
            var user = new ApplicationUser
            {
                UserName = code,
                UserCode = code,
                Email = email,
                FirstName = code,
                LastName = "Test",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, "S3cret!TestPassword");
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));
            return user;
        }

        private static Permission AddPermission(AsiBasecodeDBContext db, string name)
        {
            var permission = db.Permissions.Local.FirstOrDefault(item => item.PermissionName == name) ??
                db.Permissions.FirstOrDefault(item => item.PermissionName == name);
            if (permission != null)
            {
                return permission;
            }

            permission = new Permission
            {
                PermissionName = name,
                Description = name
            };
            db.Permissions.Add(permission);
            db.SaveChanges();
            return permission;
        }
    }
}
