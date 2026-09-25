using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Data
{
    public static class DatabaseSeeder
    {
        private static readonly IReadOnlyDictionary<string, string> PermissionDescriptions =
            new Dictionary<string, string>
            {
                [DomainValues.Permissions.EquipmentBrowse] =
                    "Browse the equipment catalog and availability calendar.",
                [DomainValues.Permissions.ReservationCreate] =
                    "Submit an equipment reservation.",
                [DomainValues.Permissions.ReservationReview] =
                    "Approve or reject reservation requests.",
                [DomainValues.Permissions.TransactionReleaseReturn] =
                    "Record equipment release and return.",
                [DomainValues.Permissions.EquipmentManage] =
                    "Maintain equipment, categories, and locations.",
                [DomainValues.Permissions.BorrowerManage] =
                    "Maintain borrower profiles and eligibility.",
                [DomainValues.Permissions.UserRoleManage] =
                    "Manage users, roles, and role permissions.",
                [DomainValues.Permissions.HistoryView] =
                    "View borrowing history and reports."
            };

        private static readonly IReadOnlyDictionary<string, string[]> RolePermissions =
            new Dictionary<string, string[]>
            {
                [DomainValues.Roles.Borrower] = new[]
                {
                    DomainValues.Permissions.EquipmentBrowse,
                    DomainValues.Permissions.ReservationCreate
                },
                [DomainValues.Roles.Custodian] = new[]
                {
                    DomainValues.Permissions.EquipmentBrowse,
                    DomainValues.Permissions.ReservationReview,
                    DomainValues.Permissions.TransactionReleaseReturn,
                    DomainValues.Permissions.HistoryView
                },
                [DomainValues.Roles.Administrator] =
                    PermissionDescriptions.Keys.ToArray()
            };

        public static async Task SeedAsync(
            IServiceProvider services,
            IConfiguration configuration)
        {
            using var scope = services.CreateScope();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("DatabaseSeeder");
            var dbContext = scope.ServiceProvider
                .GetRequiredService<AsiBasecodeDBContext>();
            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            if (!await dbContext.Database.CanConnectAsync())
            {
                throw new InvalidOperationException(
                    "Cannot connect to SQL Server. Apply the migration before running --seed.");
            }

            foreach (var roleName in RolePermissions.Keys)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole(roleName));
                    EnsureSucceeded(result, $"create role '{roleName}'");
                }
            }

            foreach (var item in PermissionDescriptions)
            {
                var permission = await dbContext.Permissions
                    .SingleOrDefaultAsync(x =>
                        x.PermissionName == item.Key);
                if (permission == null)
                {
                    dbContext.Permissions.Add(new Permission
                    {
                        PermissionName = item.Key,
                        Description = item.Value
                    });
                }
                else
                {
                    permission.Description = item.Value;
                }
            }

            await dbContext.SaveChangesAsync();

            var permissionsByName = await dbContext.Permissions
                .ToDictionaryAsync(x => x.PermissionName);

            foreach (var roleEntry in RolePermissions)
            {
                var role = await roleManager.FindByNameAsync(roleEntry.Key);
                foreach (var permissionName in roleEntry.Value)
                {
                    var permissionId =
                        permissionsByName[permissionName].PermissionId;
                    var exists = await dbContext.RolePermissions.AnyAsync(x =>
                        x.RoleId == role.Id &&
                        x.PermissionId == permissionId);
                    if (!exists)
                    {
                        dbContext.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permissionId
                        });
                    }
                }
            }

            await dbContext.SaveChangesAsync();
            await SeedAdministratorAsync(
                userManager,
                configuration,
                logger);
        }

        private static async Task SeedAdministratorAsync(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger logger)
        {
            var email = configuration["SeedAdmin:Email"];
            var password = configuration["SeedAdmin:Password"];
            var userCode = configuration["SeedAdmin:UserCode"];

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(userCode))
            {
                logger.LogInformation(
                    "SeedAdmin values are not configured; roles and permissions were seeded without an administrator account.");
                return;
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = userCode,
                    UserCode = userCode,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = configuration["SeedAdmin:FirstName"] ?? "System",
                    LastName = configuration["SeedAdmin:LastName"] ?? "Administrator",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(user, password);
                EnsureSucceeded(createResult, "create the initial administrator");
            }

            if (!await userManager.IsInRoleAsync(
                user,
                DomainValues.Roles.Administrator))
            {
                var roleResult = await userManager.AddToRoleAsync(
                    user,
                    DomainValues.Roles.Administrator);
                EnsureSucceeded(
                    roleResult,
                    "assign the Administrator role");
            }
        }

        private static void EnsureSucceeded(
            IdentityResult result,
            string operation)
        {
            if (result.Succeeded)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Failed to {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }
    }
}
