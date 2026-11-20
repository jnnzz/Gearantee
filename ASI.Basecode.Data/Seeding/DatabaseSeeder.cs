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
            // New demo accounts satisfy the current password policy if the legacy demo password predates it.
            var demoAccountPassword = string.IsNullOrWhiteSpace(demoPassword)
                ? null
                : demoPassword + "Aa1!";
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

            await GetOrAddDemoAccountAsync(
                dbContext, userManager, demoAccountPassword, "DEMO-ADMIN-001",
                "demo-admin@gearantee.local", "Morgan", "Reyes",
                DomainValues.Roles.Administrator);
            var demoCustodian = await GetOrAddDemoAccountAsync(
                dbContext, userManager, demoAccountPassword, "DEMO-CUST-001",
                "demo-custodian@gearantee.local", "Jordan", "Santos",
                DomainValues.Roles.Custodian);
            var demoBorrowers = new List<(ApplicationUser User, BorrowerProfile Profile)>();
            var borrowerSeeds = new[]
            {
                ("Ava", "Santos", "Information Technology", true),
                ("Ben", "Reyes", "Computer Science", true),
                ("Carla", "Dizon", "Engineering", true),
                ("Diego", "Lim", "Communications", true),
                ("Ella", "Cruz", "Business", true),
                ("Felix", "Ramos", "Education", true),
                ("Gia", "Flores", "Information Technology", true),
                ("Hugo", "Tan", "Computer Science", true),
                ("Isla", "Mendoza", "Engineering", true),
                ("Jace", "Navarro", "Communications", true),
                ("Kaya", "Lopez", "Business", false),
                ("Liam", "Aquino", "Education", false)
            };
            for (var index = 0; index < borrowerSeeds.Length; index++)
            {
                var number = index + 1;
                var seed = borrowerSeeds[index];
                demoBorrowers.Add(await GetOrAddDemoAccountAsync(
                    dbContext,
                    userManager,
                    demoAccountPassword,
                    $"DEMO-BRW-{number:000}",
                    $"demo-borrower-{number:000}@gearantee.local",
                    seed.Item1,
                    seed.Item2,
                    DomainValues.Roles.Borrower,
                    seed.Item3,
                    seed.Item4));
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

            await SeedExpandedDemoDataAsync(
                dbContext,
                demoCustodian.User,
                demoBorrowers,
                laptopCategory,
                projectorCategory,
                cameraCategory,
                mainLocation,
                stageLocation);

                await transaction.CommitAsync();
            });
            logger.LogInformation(
                "Development demo data is ready. Use DEMO-ADMIN-001, DEMO-CUST-001, DEMO-BRW-001 through DEMO-BRW-012, or the multi-role account {UserCode}.",
                user.UserCode);
        }

        private static async Task<(ApplicationUser User, BorrowerProfile Profile)> GetOrAddDemoAccountAsync(
            AsiBasecodeDBContext dbContext,
            UserManager<ApplicationUser> userManager,
            string password,
            string userCode,
            string email,
            string firstName,
            string lastName,
            string roleName,
            string department = null,
            bool isEligible = false)
        {
            var accountByCode = await userManager.FindByNameAsync(userCode);
            var accountByEmail = await userManager.FindByEmailAsync(email);
            if (accountByCode != null && accountByEmail != null &&
                accountByCode.Id != accountByEmail.Id)
            {
                throw new InvalidOperationException(
                    $"Demo account identifiers for '{userCode}' conflict with existing accounts.");
            }

            var user = accountByCode ?? accountByEmail;
            if (user == null)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException(
                        "Set SeedDemo:Password with user-secrets before running --seed-demo.");
                }

                user = new ApplicationUser
                {
                    UserName = userCode,
                    UserCode = userCode,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                EnsureSucceeded(
                    await userManager.CreateAsync(user, password),
                    $"create demo account '{userCode}'");
            }
            else if (!string.Equals(user.UserCode, userCode, StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Demo account identifiers for '{userCode}' conflict with an existing account.");
            }

            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                EnsureSucceeded(
                    await userManager.AddToRoleAsync(user, roleName),
                    $"assign the {roleName} role to demo account '{userCode}'");
            }

            BorrowerProfile profile = null;
            if (roleName == DomainValues.Roles.Borrower)
            {
                profile = await dbContext.BorrowerProfiles
                    .SingleOrDefaultAsync(item => item.UserId == user.Id);
                if (profile == null)
                {
                    profile = new BorrowerProfile
                    {
                        UserId = user.Id,
                        Department = department,
                        ContactNumber = "000-000-0000",
                        IsEligible = isEligible,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    dbContext.BorrowerProfiles.Add(profile);
                    await dbContext.SaveChangesAsync();
                }
            }

            return (user, profile);
        }

        private static async Task SeedExpandedDemoDataAsync(
            AsiBasecodeDBContext dbContext,
            ApplicationUser custodian,
            IReadOnlyList<(ApplicationUser User, BorrowerProfile Profile)> borrowers,
            EquipmentCategory laptopCategory,
            EquipmentCategory projectorCategory,
            EquipmentCategory cameraCategory,
            string mainLocation,
            string stageLocation)
        {
            var audioCategory = await GetOrAddCategoryAsync(dbContext, "DEMO-AUD", "Audio");
            var tabletCategory = await GetOrAddCategoryAsync(dbContext, "DEMO-TAB", "Tablets");
            var categories = new Dictionary<string, EquipmentCategory>
            {
                ["LAP"] = laptopCategory,
                ["PROJ"] = projectorCategory,
                ["CAM"] = cameraCategory,
                ["AUD"] = audioCategory,
                ["TAB"] = tabletCategory
            };
            var definitions = new (string Code, string Name, string Category, string Location, string Status)[]
            {
                ("DEMO-LAP-005", "Lenovo ThinkPad T14", "LAP", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-LAP-006", "Dell Latitude 7440", "LAP", stageLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-LAP-007", "HP EliteBook 840", "LAP", mainLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-LAP-008", "Acer Swift 3", "LAP", stageLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-LAP-009", "Microsoft Surface Laptop 5", "LAP", mainLocation, DomainValues.EquipmentStatuses.UnderMaintenance),
                ("DEMO-LAP-010", "ASUS ExpertBook B1", "LAP", stageLocation, DomainValues.EquipmentStatuses.Unavailable),
                ("DEMO-LAP-011", "Lenovo IdeaPad Slim 5", "LAP", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-LAP-012", "Dell Inspiron 14", "LAP", stageLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-PROJ-003", "ViewSonic PA503W", "PROJ", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-PROJ-004", "Epson PowerLite 982W", "PROJ", stageLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-PROJ-005", "BenQ TH585", "PROJ", mainLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-PROJ-006", "Optoma HD146X", "PROJ", stageLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-PROJ-007", "Epson EB-W06", "PROJ", mainLocation, DomainValues.EquipmentStatuses.UnderMaintenance),
                ("DEMO-PROJ-008", "Acer X1328WH", "PROJ", stageLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-CAM-003", "Canon EOS M50", "CAM", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-CAM-004", "Nikon D3500", "CAM", stageLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-CAM-005", "Sony Alpha a6400", "CAM", mainLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-CAM-006", "Canon EOS R50", "CAM", stageLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-CAM-007", "Fujifilm X-T30", "CAM", mainLocation, DomainValues.EquipmentStatuses.Unavailable),
                ("DEMO-CAM-008", "Panasonic Lumix G7", "CAM", stageLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-AUD-001", "JBL EON610 Speaker", "AUD", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-AUD-002", "Shure BLX Wireless Microphone", "AUD", stageLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-AUD-003", "Yamaha MG10XU Mixer", "AUD", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-AUD-004", "Bose S1 Pro Speaker", "AUD", stageLocation, DomainValues.EquipmentStatuses.UnderMaintenance),
                ("DEMO-TAB-001", "Apple iPad 10th Gen", "TAB", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-TAB-002", "Samsung Galaxy Tab S9", "TAB", stageLocation, DomainValues.EquipmentStatuses.Borrowed),
                ("DEMO-TAB-003", "Microsoft Surface Go 4", "TAB", mainLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-TAB-004", "Lenovo Tab P12", "TAB", stageLocation, DomainValues.EquipmentStatuses.Available),
                ("DEMO-TAB-005", "Huawei MatePad 11", "TAB", mainLocation, DomainValues.EquipmentStatuses.UnderMaintenance),
                ("DEMO-TAB-006", "Xiaomi Pad 6", "TAB", stageLocation, DomainValues.EquipmentStatuses.Unavailable)
            };
            var items = new Dictionary<string, EquipmentItem>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                items[definition.Code] = await GetOrAddEquipmentAsync(
                    dbContext,
                    definition.Code,
                    definition.Name,
                    categories[definition.Category],
                    definition.Location,
                    definition.Status);
            }

            if (await dbContext.Reservations.AnyAsync(item =>
                    item.Purpose.StartsWith("DEMO-DEFENSE:")))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(now, ManilaTimeZone).Date;
            var todayStart = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(todayLocal, DateTimeKind.Unspecified), ManilaTimeZone);
            var pendingCodes = new[]
            {
                "DEMO-LAP-005", "DEMO-LAP-008", "DEMO-LAP-011", "DEMO-LAP-012",
                "DEMO-PROJ-003", "DEMO-PROJ-006", "DEMO-PROJ-008", "DEMO-CAM-003",
                "DEMO-CAM-006", "DEMO-CAM-008"
            };
            var pending = new List<Reservation>();
            for (var index = 0; index < pendingCodes.Length; index++)
            {
                var start = todayStart.AddDays(index + 1).AddHours(9 + index % 4);
                var requestedAt = now.AddMinutes(-index - 1);
                var reservation = DemoReservation(
                    borrowers[index % borrowers.Count].Profile,
                    items[pendingCodes[index]],
                    start,
                    start.AddHours(2 + index % 3),
                    "Pending",
                    $"DEMO-DEFENSE: Pending request {index + 1:00}");
                reservation.RequestedAt = requestedAt;
                reservation.CreatedAt = requestedAt;
                pending.Add(reservation);
            }

            var approvedCodes = new[]
            {
                "DEMO-AUD-001", "DEMO-AUD-003", "DEMO-TAB-001",
                "DEMO-TAB-003", "DEMO-TAB-004", "DEMO-PROJ-006"
            };
            var approvedPickups = new List<Reservation>();
            for (var index = 0; index < approvedCodes.Length; index++)
            {
                var start = todayStart.AddDays(index + 2).AddHours(10 + index % 3);
                var reservation = DemoReservation(
                    borrowers[(index + 2) % borrowers.Count].Profile,
                    items[approvedCodes[index]],
                    start,
                    start.AddHours(3),
                    "Approved",
                    $"DEMO-DEFENSE: Approved pickup {index + 1:00}",
                    custodian.Id,
                    now.AddHours(-1));
                reservation.RequestedAt = now.AddDays(-3);
                reservation.CreatedAt = reservation.RequestedAt;
                approvedPickups.Add(reservation);
            }

            var rejectedCodes = new[]
            {
                "DEMO-LAP-005", "DEMO-PROJ-003", "DEMO-CAM-003", "DEMO-TAB-001"
            };
            var rejected = new List<Reservation>();
            for (var index = 0; index < rejectedCodes.Length; index++)
            {
                var start = todayStart.AddDays(index + 2).AddHours(14);
                var reservation = DemoReservation(
                    borrowers[(index + 5) % borrowers.Count].Profile,
                    items[rejectedCodes[index]],
                    start,
                    start.AddHours(2),
                    "Rejected",
                    $"DEMO-DEFENSE: Rejected request {index + 1:00}",
                    custodian.Id,
                    now.AddDays(-1),
                    "Please choose another available time.");
                reservation.RequestedAt = now.AddDays(-2);
                reservation.CreatedAt = reservation.RequestedAt;
                rejected.Add(reservation);
            }

            dbContext.Reservations.AddRange(pending);
            dbContext.Reservations.AddRange(approvedPickups);
            dbContext.Reservations.AddRange(rejected);

            var borrowedCodes = new[]
            {
                "DEMO-LAP-006", "DEMO-LAP-007", "DEMO-PROJ-004", "DEMO-PROJ-005",
                "DEMO-CAM-004", "DEMO-CAM-005", "DEMO-AUD-002", "DEMO-TAB-002"
            };
            var activeLoans = new List<Reservation>();
            for (var index = 0; index < borrowedCodes.Length; index++)
            {
                var overdue = index >= 5;
                var start = overdue
                    ? now.AddDays(-5 - index % 3)
                    : now.AddHours(-2 - index);
                var end = overdue
                    ? now.AddDays(-1 - index % 2)
                    : now.AddDays(2 + index % 3);
                var requestedAt = start.AddDays(-5);
                var reservation = DemoReservation(
                    borrowers[index % borrowers.Count].Profile,
                    items[borrowedCodes[index]],
                    start,
                    end,
                    "Approved",
                    $"DEMO-DEFENSE: {(overdue ? "Overdue" : "Active")} loan {index + 1:00}",
                    custodian.Id,
                    requestedAt.AddDays(2));
                reservation.RequestedAt = requestedAt;
                reservation.CreatedAt = requestedAt;
                activeLoans.Add(reservation);
            }
            dbContext.Reservations.AddRange(activeLoans);
            await dbContext.SaveChangesAsync();

            var activeLoanReleases = activeLoans.Select(reservation => DemoRelease(
                reservation,
                custodian.Id,
                reservation.ReservationStart)).ToList();
            dbContext.ReleaseRecords.AddRange(activeLoanReleases);
            await dbContext.SaveChangesAsync();

            var returnedCodes = new[]
            {
                "DEMO-LAP-005", "DEMO-LAP-008", "DEMO-LAP-011", "DEMO-LAP-012",
                "DEMO-PROJ-003", "DEMO-PROJ-006", "DEMO-CAM-003", "DEMO-CAM-006",
                "DEMO-PROJ-008", "DEMO-CAM-008", "DEMO-LAP-009", "DEMO-PROJ-007"
            };
            var lateIndexes = new HashSet<int> { 8, 9 };
            var damagedIndexes = new HashSet<int> { 10, 11 };
            var completed = new List<(Reservation Reservation, ReturnRecord Return, int DaysLate)>();
            for (var index = 0; index < returnedCodes.Length; index++)
            {
                var start = todayStart.AddDays(-7 - index * 2).AddHours(10 + index % 4);
                var end = start.AddHours(3);
                var requestedAt = start.AddDays(-4);
                var reservation = DemoReservation(
                    borrowers[(index + 3) % borrowers.Count].Profile,
                    items[returnedCodes[index]],
                    start,
                    end,
                    "Approved",
                    $"DEMO-DEFENSE: Completed return {index + 1:00}",
                    custodian.Id,
                    requestedAt.AddDays(2));
                reservation.RequestedAt = requestedAt;
                reservation.CreatedAt = requestedAt;
                var daysLate = lateIndexes.Contains(index) ? 2 : 0;
                var isDamaged = damagedIndexes.Contains(index);
                var actualReturn = end.AddDays(daysLate);
                var returnedCondition = isDamaged
                    ? DomainValues.ReturnConditions.Damaged
                    : DomainValues.ReturnConditions.Good;
                var resultingStatus = isDamaged
                    ? DomainValues.EquipmentStatuses.UnderMaintenance
                    : DomainValues.EquipmentStatuses.Available;
                var returnRecord = new ReturnRecord
                {
                    ReceivedByUserId = custodian.Id,
                    ActualReturnAt = actualReturn,
                    ReturnedCondition = returnedCondition,
                    ResultingItemStatus = resultingStatus,
                    Notes = $"DEMO-DEFENSE: {(isDamaged ? "Damaged" : daysLate > 0 ? "Late" : "Good")} return {index + 1:00}"
                };
                completed.Add((reservation, returnRecord, daysLate));
            }

            dbContext.Reservations.AddRange(completed.Select(item => item.Reservation));
            await dbContext.SaveChangesAsync();
            var completedReleases = completed.Select(item => DemoRelease(
                item.Reservation,
                custodian.Id,
                item.Reservation.ReservationStart)).ToList();
            dbContext.ReleaseRecords.AddRange(completedReleases);
            await dbContext.SaveChangesAsync();
            for (var index = 0; index < completed.Count; index++)
            {
                completed[index].Return.ReleaseRecordId = completedReleases[index].ReleaseRecordId;
            }
            dbContext.ReturnRecords.AddRange(completed.Select(item => item.Return));
            await dbContext.SaveChangesAsync();

            foreach (var item in completed.Where(item => item.DaysLate > 0))
            {
                dbContext.LateReturns.Add(new LateReturn
                {
                    ReturnRecordId = item.Return.ReturnRecordId,
                    DueAt = item.Reservation.ReservationEnd,
                    ReturnedAt = item.Return.ActualReturnAt,
                    DaysLate = item.DaysLate,
                    PenaltyStatus = DomainValues.PenaltyStatuses.Recorded,
                    Notes = "DEMO-DEFENSE: Sample late-return history."
                });
            }
            await dbContext.SaveChangesAsync();
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
