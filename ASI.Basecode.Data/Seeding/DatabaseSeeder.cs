using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Seeding
{
    public static class DatabaseSeeder
    {
        private static readonly TimeZoneInfo ManilaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

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
                    PermissionDescriptions.Keys
                        .Where(permission => permission !=
                            DomainValues.Permissions.ReservationCreate)
                        .ToArray()
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
                var defaultsWereSeeded = await dbContext.RolePermissionSeeds
                    .AnyAsync(seed => seed.RoleId == role.Id);
                if (defaultsWereSeeded)
                {
                    continue;
                }

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

                dbContext.RolePermissionSeeds.Add(new RolePermissionSeed
                {
                    RoleId = role.Id
                });
            }

            await dbContext.SaveChangesAsync();
            await SeedAdministratorAsync(
                userManager,
                configuration,
                logger);
            await SeedCustodianAsync(userManager, configuration, logger);
        }

        private static async Task SeedCustodianAsync(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger logger)
        {
            var email = configuration["SeedCustodian:Email"];
            var password = configuration["SeedCustodian:Password"];
            var userCode = configuration["SeedCustodian:UserCode"];

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(userCode))
            {
                logger.LogInformation(
                    "SeedCustodian values are not configured; skipping custodian seeding. Configure SeedCustodian:Email, SeedCustodian:Password, and SeedCustodian:UserCode explicitly.");
                return;
            }

            // Reseeding must not reset credentials, reactivate accounts or restore revoked roles.
            if (await userManager.FindByEmailAsync(email) != null ||
                await userManager.FindByNameAsync(userCode) != null)
            {
                logger.LogInformation("Custodian seed account already exists; leaving it unchanged.");
                return;
            }

            var user = new ApplicationUser
            {
                UserName = userCode,
                UserCode = userCode,
                Email = email,
                EmailConfirmed = true,
                FirstName = configuration["SeedCustodian:FirstName"] ?? "Equipment",
                LastName = configuration["SeedCustodian:LastName"] ?? "Custodian",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password), "create the initial custodian");
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, DomainValues.Roles.Custodian),
                "assign the Custodian role");
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
                logger.LogWarning(
                    "SeedAdmin values are not configured; skipping administrator seeding. Configure SeedAdmin:Email, SeedAdmin:Password, and SeedAdmin:UserCode explicitly.");
                return;
            }

            var user = await userManager.FindByEmailAsync(email);
            var created = user == null;
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

            if (created && !await userManager.IsInRoleAsync(user, DomainValues.Roles.Administrator))
            {
                var roleResult = await userManager.AddToRoleAsync(
                    user,
                    DomainValues.Roles.Administrator);
                EnsureSucceeded(roleResult, "assign the Administrator role");
            }
        }
        public static async Task SeedDemoAsync(
            IServiceProvider services,
            IConfiguration configuration)
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;
            var dbContext = provider.GetRequiredService<AsiBasecodeDBContext>();
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = provider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("DatabaseSeeder.Demo");

            if (!await dbContext.Database.CanConnectAsync())
            {
                throw new InvalidOperationException(
                    "Cannot connect to SQL Server. Apply the migration before running --seed-demo.");
            }

            var demoEmail = configuration["SeedDemo:Email"] ?? "demo@gearantee.local";
            var demoUserCode = configuration["SeedDemo:UserCode"] ?? "DEMO-001";
            var demoPassword = configuration["SeedDemo:Password"];
            var user = await userManager.FindByEmailAsync(demoEmail);
            if (user == null)
            {
                if (string.IsNullOrWhiteSpace(demoPassword))
                {
                    throw new InvalidOperationException(
                        "Set SeedDemo:Password with user-secrets before running --seed-demo.");
                }

                user = new ApplicationUser
                {
                    UserName = demoUserCode,
                    UserCode = demoUserCode,
                    Email = demoEmail,
                    EmailConfirmed = true,
                    FirstName = "Demo",
                    LastName = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                EnsureSucceeded(
                    await userManager.CreateAsync(user, demoPassword),
                    "create the development demo account");
            }

            foreach (var roleName in RolePermissions.Keys)
            {
                if (!await userManager.IsInRoleAsync(user, roleName))
                {
                    EnsureSucceeded(
                        await userManager.AddToRoleAsync(user, roleName),
                        $"assign the {roleName} role to the demo account");
                }
            }

            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var profile = await dbContext.BorrowerProfiles
                .SingleOrDefaultAsync(item => item.UserId == user.Id);
            if (profile == null)
            {
                profile = new BorrowerProfile
                {
                    UserId = user.Id,
                    Department = "Information Technology",
                    ContactNumber = "000-000-0000",
                    IsEligible = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.BorrowerProfiles.Add(profile);
                await dbContext.SaveChangesAsync();
            }

            var laptopCategory = await GetOrAddCategoryAsync(
                dbContext, "DEMO-LAP", "Laptops");
            var projectorCategory = await GetOrAddCategoryAsync(
                dbContext, "DEMO-PROJ", "Projectors");
            var cameraCategory = await GetOrAddCategoryAsync(
                dbContext, "DEMO-CAM", "Cameras");
            const string mainLocation = "Main Equipment Room";
            const string stageLocation = "Stage Store";

            var dueItem = await GetOrAddEquipmentAsync(
                dbContext, "DEMO-LAP-001", "Lenovo ThinkPad L14", laptopCategory,
                mainLocation, DomainValues.EquipmentStatuses.Borrowed);
            var pickupItem = await GetOrAddEquipmentAsync(
                dbContext, "DEMO-LAP-002", "Dell Latitude 5440", laptopCategory,
                stageLocation, DomainValues.EquipmentStatuses.Available);
            var pendingItem = await GetOrAddEquipmentAsync(
                dbContext, "DEMO-LAP-003", "HP ProBook 440", laptopCategory,
                mainLocation, DomainValues.EquipmentStatuses.Available);
            var damagedItem = await GetOrAddEquipmentAsync(
                dbContext, "DEMO-LAP-004", "Acer TravelMate P2", laptopCategory,
                mainLocation, DomainValues.EquipmentStatuses.UnderMaintenance);
            var overdueItem = await GetOrAddEquipmentAsync(
                dbContext, "DEMO-PROJ-001", "Epson EB-X51", projectorCategory,
                stageLocation, DomainValues.EquipmentStatuses.Borrowed);
            var secondProjector = await GetOrAddEquipmentAsync(
                dbContext, "DEMO-PROJ-002", "BenQ MW560", projectorCategory,
                mainLocation, DomainValues.EquipmentStatuses.Available);
            var cameraOnLoan = await GetOrAddEquipmentAsync(
                dbContext, "DEMO-CAM-001", "Canon EOS 250D", cameraCategory,
                mainLocation, DomainValues.EquipmentStatuses.Borrowed);
            await GetOrAddEquipmentAsync(
                dbContext, "DEMO-CAM-002", "Sony Handycam", cameraCategory,
                stageLocation, DomainValues.EquipmentStatuses.Unavailable);

            var hasDemoReservations = await dbContext.Reservations
                .AnyAsync(item => item.Purpose.StartsWith("DEMO:"));
            if (!hasDemoReservations)
            {
                var now = DateTime.UtcNow;
                // Same "Philippine business day" window the seeded loans are expected to fall in.
                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(now, ManilaTimeZone).Date;
                var todayStart = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(todayLocal, DateTimeKind.Unspecified), ManilaTimeZone);
                var todayEnd = todayStart.AddDays(1);
                var pendingOne = DemoReservation(profile, pendingItem, now.AddDays(1), now.AddDays(1).AddHours(3), "Pending", "DEMO: Pending laptop request");
                var pendingTwo = DemoReservation(profile, secondProjector, now.AddDays(2), now.AddDays(2).AddHours(2), "Pending", "DEMO: Pending projector request");
                var pendingThree = DemoReservation(profile, pendingItem, now.AddDays(5), now.AddDays(5).AddHours(3), "Pending", "DEMO: Second pending laptop request");
                var pickup = DemoReservation(profile, pickupItem, todayStart.AddHours(18), todayStart.AddHours(20), "Approved", "DEMO: Approved pickup today", user.Id, now);
                var dueToday = DemoReservation(profile, dueItem, todayStart.AddHours(-3), todayEnd.AddHours(-1), "Approved", "DEMO: Active loan due today", user.Id, now.AddDays(-1));
                var overdue = DemoReservation(profile, overdueItem, now.AddDays(-3), now.AddHours(-2), "Approved", "DEMO: Active overdue loan", user.Id, now.AddDays(-3));
                var cameraLoan = DemoReservation(profile, cameraOnLoan, now.AddHours(-2), now.AddDays(2), "Approved", "DEMO: Active camera loan", user.Id, now.AddHours(-2));
                var rejected = DemoReservation(profile, pendingItem, now.AddDays(3), now.AddDays(3).AddHours(2), "Rejected", "DEMO: Rejected reservation", user.Id, now, "Please select a different time slot.");
                var returned = DemoReservation(profile, damagedItem, now.AddDays(-10), now.AddDays(-10).AddHours(3), "Approved", "DEMO: Completed equipment return", user.Id, now.AddDays(-10));
                var late = DemoReservation(profile, damagedItem, now.AddDays(-8), now.AddDays(-8).AddHours(3), "Approved", "DEMO: Late equipment return", user.Id, now.AddDays(-8));
                var damaged = DemoReservation(profile, damagedItem, now.AddDays(-6), now.AddDays(-6).AddHours(3), "Approved", "DEMO: Damaged equipment return", user.Id, now.AddDays(-6));

                dbContext.Reservations.AddRange(
                    pendingOne, pendingTwo, pendingThree, pickup, dueToday, overdue,
                    cameraLoan, rejected, returned, late, damaged);
                await dbContext.SaveChangesAsync();

                var dueRelease = DemoRelease(dueToday, user.Id, todayStart.AddHours(-3));
                var overdueRelease = DemoRelease(overdue, user.Id, now.AddDays(-3));
                var cameraRelease = DemoRelease(cameraLoan, user.Id, now.AddHours(-2));
                var returnedRelease = DemoRelease(returned, user.Id, now.AddDays(-10));
                var lateRelease = DemoRelease(late, user.Id, now.AddDays(-8));
                var damagedRelease = DemoRelease(damaged, user.Id, now.AddDays(-6));
                dbContext.ReleaseRecords.AddRange(
                    dueRelease, overdueRelease, cameraRelease,
                    returnedRelease, lateRelease, damagedRelease);
                await dbContext.SaveChangesAsync();

                dbContext.ReturnRecords.AddRange(
                    new ReturnRecord
                    {
                        ReleaseRecordId = returnedRelease.ReleaseRecordId,
                        ReceivedByUserId = user.Id,
                        ActualReturnAt = returned.ReservationEnd,
                        ReturnedCondition = DomainValues.ReturnConditions.Good,
                        ResultingItemStatus = DomainValues.EquipmentStatuses.Available,
                        Notes = "DEMO: Returned in good condition."
                    },
                    new ReturnRecord
                    {
                        ReleaseRecordId = lateRelease.ReleaseRecordId,
                        ReceivedByUserId = user.Id,
                        ActualReturnAt = late.ReservationEnd.AddDays(2),
                        ReturnedCondition = DomainValues.ReturnConditions.Good,
                        ResultingItemStatus = DomainValues.EquipmentStatuses.Available,
                        Notes = "DEMO: Returned after due time."
                    },
                    new ReturnRecord
                    {
                        ReleaseRecordId = damagedRelease.ReleaseRecordId,
                        ReceivedByUserId = user.Id,
                        ActualReturnAt = damaged.ReservationEnd.AddHours(1),
                        ReturnedCondition = DomainValues.ReturnConditions.Damaged,
                        ResultingItemStatus = DomainValues.EquipmentStatuses.UnderMaintenance,
                        Notes = "DEMO: Needs inspection."
                    });
                await dbContext.SaveChangesAsync();

                var lateReturnRecord = await dbContext.ReturnRecords
                    .SingleAsync(item => item.Notes == "DEMO: Returned after due time.");
                dbContext.LateReturns.Add(new LateReturn
                {
                    ReturnRecordId = lateReturnRecord.ReturnRecordId,
                    DueAt = late.ReservationEnd,
                    ReturnedAt = late.ReservationEnd.AddDays(2),
                    DaysLate = 2,
                    PenaltyStatus = DomainValues.PenaltyStatuses.Recorded,
                    Notes = "DEMO: Sample late-return history."
                });
                await dbContext.SaveChangesAsync();
            }

                await transaction.CommitAsync();
            });
            logger.LogInformation(
                "Development demo data is ready for {UserCode}. The demo account has Borrower, Custodian, and Administrator roles.",
                user.UserCode);
        }

        private static async Task<EquipmentCategory> GetOrAddCategoryAsync(
            AsiBasecodeDBContext dbContext,
            string code,
            string name)
        {
            var category = await dbContext.EquipmentCategories
                .SingleOrDefaultAsync(item => item.CategoryCode == code);
            category ??= await dbContext.EquipmentCategories
                .SingleOrDefaultAsync(item => item.CategoryName == name);
            if (category != null)
            {
                return category;
            }

            category = new EquipmentCategory
            {
                CategoryCode = code,
                CategoryName = name,
                Description = "Development sample data.",
                IsActive = true
            };
            dbContext.EquipmentCategories.Add(category);
            await dbContext.SaveChangesAsync();
            return category;
        }

        private static async Task<EquipmentItem> GetOrAddEquipmentAsync(
            AsiBasecodeDBContext dbContext,
            string code,
            string name,
            EquipmentCategory category,
            string location,
            string status)
        {
            var item = await dbContext.EquipmentItems
                .SingleOrDefaultAsync(existing => existing.ItemCode == code);
            if (item != null)
            {
                return item;
            }

            item = new EquipmentItem
            {
                CategoryId = category.CategoryId,
                Location = location,
                ItemCode = code,
                ItemName = name,
                Description = "Seeded sample equipment for dashboard previews.",
                Brand = name.Split(' ')[0],
                Model = name,
                SerialNumber = code + "-SN",
                ConditionStatus = status == DomainValues.EquipmentStatuses.UnderMaintenance
                    ? "Damaged"
                    : "Good",
                ItemStatus = status,
                IsArchived = false
            };
            dbContext.EquipmentItems.Add(item);
            await dbContext.SaveChangesAsync();
            return item;
        }

        private static Reservation DemoReservation(
            BorrowerProfile profile,
            EquipmentItem item,
            DateTime start,
            DateTime end,
            string status,
            string purpose,
            string reviewedByUserId = null,
            DateTime? reviewedAt = null,
            string rejectionReason = null)
        {
            return new Reservation
            {
                BorrowerProfileId = profile.BorrowerProfileId,
                EquipmentId = item.EquipmentId,
                RequestedAt = DateTime.UtcNow,
                ReservationStart = start,
                ReservationEnd = end,
                Purpose = purpose,
                Status = status,
                ReviewedByUserId = reviewedByUserId,
                ReviewedAt = reviewedAt,
                RejectionReason = rejectionReason,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static ReleaseRecord DemoRelease(
            Reservation reservation,
            string userId,
            DateTime releasedAt)
        {
            return new ReleaseRecord
            {
                ReservationId = reservation.ReservationId,
                ReleasedByUserId = userId,
                ActualReleaseAt = releasedAt,
                Notes = "DEMO: Development dashboard sample.",
                CreatedAt = releasedAt
            };
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
