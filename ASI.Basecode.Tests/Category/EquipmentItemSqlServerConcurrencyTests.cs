using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Reservations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class EquipmentItemSqlServerConcurrencyTests
    {
        [InlineData(false)]
        [InlineData(true)]
        [SqlServerTheory]
        public async Task ConcurrentInventoryChangeAndReservationCannotCommitConflictingState(
            bool archiveInventoryItem)
        {
            var databaseName = "Gearantee_EquipmentRace_" + Guid.NewGuid().ToString("N");
            var connectionString =
                $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=30";
            var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                .UseSqlServer(connectionString)
                .Options;

            try
            {
                string adminId;
                long categoryId;
                long equipmentId;
                string borrowerUserId;
                string itemCode;

                await using (var setup = new AsiBasecodeDBContext(options))
                {
                    await setup.Database.MigrateAsync();
                    (adminId, categoryId, equipmentId, borrowerUserId, itemCode) =
                        await SeedAsync(setup);
                }

                using var loggerFactory = LoggerFactory.Create(builder => { });
                var inventorySaveGate = new EquipmentSavePauseInterceptor();
                var reservationLockObserver = new EquipmentLockCommandObserver();
                var inventoryOptions = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                    .UseSqlServer(connectionString)
                    .AddInterceptors(inventorySaveGate)
                    .Options;
                var reservationOptions = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                    .UseSqlServer(connectionString)
                    .AddInterceptors(reservationLockObserver)
                    .Options;
                var nowUtc = DateTime.UtcNow;

                var inventoryTask = Task.Run(async () =>
                {
                    await using var db = new AsiBasecodeDBContext(inventoryOptions);
                    var categories = new CategoryService(new UnitOfWork(db), loggerFactory);
                    var service = new EquipmentItemService(
                        new UnitOfWork(db), categories, new EquipmentItemRepository(new UnitOfWork(db)), loggerFactory);
                    if (archiveInventoryItem)
                    {
                        return await service.SetArchivedAsync(adminId, equipmentId, true);
                    }

                    return await service.UpdateAsync(adminId, new EquipmentItemFormViewModel
                    {
                        EquipmentId = equipmentId,
                        CategoryId = categoryId,
                        ItemCode = itemCode,
                        ItemName = "Concurrency test item",
                        Location = "Test store",
                        ConditionStatus = DomainValues.ReturnConditions.Good,
                        ItemStatus = DomainValues.EquipmentStatuses.UnderMaintenance
                    });
                });

                Task<ReservationResult> reservationTask = null;
                var lockQueryWasBlocked = false;
                try
                {
                    await inventorySaveGate.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));

                    // Exercise the production submit path while inventory holds the shared
                    // row lock after its reservation check but before writing the item change.
                    reservationTask = Task.Run(() =>
                    {
                        using var db = new AsiBasecodeDBContext(reservationOptions);
                        var unitOfWork = new UnitOfWork(db);
                        var service = new ReservationService(
                            unitOfWork,
                            new ReservationRepository(unitOfWork),
                            new EquipmentItemRepository(unitOfWork),
                            new BorrowerProfileRepository(unitOfWork),
                            new AuditService(unitOfWork),
                            new ReservationTestHelpers.FixedTimeProvider(nowUtc));
                        return service.SubmitReservation(new ReservationCreateViewModel
                        {
                            EquipmentId = equipmentId,
                            ReservationStart = ManilaClock.ToLocal(nowUtc.AddHours(1)),
                            ReservationEnd = ManilaClock.ToLocal(nowUtc.AddHours(2)),
                            Purpose = "SQL Server concurrency regression test"
                        }, borrowerUserId);
                    });

                    await reservationLockObserver.LockQueryStarted.Task
                        .WaitAsync(TimeSpan.FromSeconds(30));
                    await Task.Delay(TimeSpan.FromMilliseconds(250));
                    lockQueryWasBlocked = !reservationLockObserver.LockQueryCompleted.Task.IsCompleted;
                }
                finally
                {
                    inventorySaveGate.Release();
                }

                await inventoryTask.WaitAsync(TimeSpan.FromSeconds(45));
                Assert.NotNull(reservationTask);
                await reservationTask.WaitAsync(TimeSpan.FromSeconds(45));

                var inventoryResult = await inventoryTask;
                var reservationResult = await reservationTask;
                Assert.True(lockQueryWasBlocked,
                    "The reservation workflow should wait for the in-flight inventory transaction's per-item lock.");
                Assert.True(inventoryResult.Succeeded, inventoryResult.Message);
                Assert.False(reservationResult.Succeeded, reservationResult.Message);
                Assert.Contains("can't be reserved", reservationResult.Message);

                await using var verify = new AsiBasecodeDBContext(options);
                var finalItem = await verify.EquipmentItems.AsNoTracking()
                    .SingleAsync(item => item.EquipmentId == equipmentId);
                var pendingCount = await verify.Reservations.AsNoTracking()
                    .CountAsync(reservation => reservation.EquipmentId == equipmentId &&
                        reservation.Status == DomainValues.ReservationStatuses.Pending &&
                        reservation.ReservationEnd > DateTime.UtcNow);

                Assert.Equal(archiveInventoryItem, finalItem.IsArchived);
                if (!archiveInventoryItem)
                {
                    Assert.Equal(DomainValues.EquipmentStatuses.UnderMaintenance, finalItem.ItemStatus);
                }
                Assert.Equal(0, pendingCount);
            }
            finally
            {
                await using var cleanup = new AsiBasecodeDBContext(options);
                await cleanup.Database.EnsureDeletedAsync();
            }
        }

        private static async Task<(string AdminId, long CategoryId,
            long EquipmentId, string BorrowerUserId, string ItemCode)> SeedAsync(
            AsiBasecodeDBContext db)
        {
            var adminId = Guid.NewGuid().ToString("N");
            var borrowerId = Guid.NewGuid().ToString("N");
            var roleId = Guid.NewGuid().ToString("N");
            var role = new IdentityRole
            {
                Id = roleId,
                Name = DomainValues.Roles.Administrator,
                NormalizedName = DomainValues.Roles.Administrator.ToUpperInvariant()
            };
            var permission = new Permission
            {
                PermissionName = DomainValues.Permissions.EquipmentManage,
                Description = "Manage equipment for SQL Server concurrency tests"
            };
            var admin = NewUser(adminId, "ADMIN-RACE");
            var borrower = NewUser(borrowerId, "BORROWER-RACE");
            var category = new EquipmentCategory
            {
                CategoryCode = "RACE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                CategoryName = "Concurrency test equipment",
                IsActive = true
            };
            var itemCode = "RACE-ITEM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var item = new EquipmentItem
            {
                Category = category,
                ItemCode = itemCode,
                ItemName = "Concurrency test item",
                Location = "Test store",
                ConditionStatus = DomainValues.ReturnConditions.Good,
                ItemStatus = DomainValues.EquipmentStatuses.Available
            };
            var profile = new BorrowerProfile
            {
                User = borrower,
                Department = "Concurrency Tests",
                IsEligible = true
            };

            db.Roles.Add(role);
            db.Users.AddRange(admin, borrower);
            db.Permissions.Add(permission);
            db.UserRoles.Add(new IdentityUserRole<string> { RoleId = roleId, UserId = adminId });
            db.EquipmentItems.Add(item);
            db.BorrowerProfiles.Add(profile);
            await db.SaveChangesAsync();

            db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.PermissionId
            });
            await db.SaveChangesAsync();

            return (adminId, category.CategoryId, item.EquipmentId, borrowerId, itemCode);
        }

        private static ApplicationUser NewUser(string id, string code) => new()
        {
            Id = id,
            UserName = code,
            NormalizedUserName = code.ToUpperInvariant(),
            Email = code.ToLowerInvariant() + "@test.local",
            NormalizedEmail = code.ToUpperInvariant() + "@TEST.LOCAL",
            UserCode = code,
            FirstName = code,
            LastName = "Test",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
    }

    internal sealed class EquipmentSavePauseInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SaveStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => release.TrySetResult(true);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<EquipmentItem>()
                    .Any(entry => entry.State == EntityState.Modified) == true)
            {
                SaveStarted.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return result;
        }
    }

    internal sealed class EquipmentLockCommandObserver : DbCommandInterceptor
    {
        public TaskCompletionSource<bool> LockQueryStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> LockQueryCompleted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ObserveStarted(command);
            return result;
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            ObserveCompleted(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ObserveStarted(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            ObserveCompleted(command);
            return ValueTask.FromResult(result);
        }

        private void ObserveStarted(DbCommand command)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                LockQueryStarted.TrySetResult(true);
            }
        }

        private void ObserveCompleted(DbCommand command)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                LockQueryCompleted.TrySetResult(true);
            }
        }
    }

    public sealed class SqlServerTheoryAttribute : TheoryAttribute
    {
        public SqlServerTheoryAttribute()
        {
            if (Environment.GetEnvironmentVariable("GEARANTEE_TEST_SQLSERVER") != "1")
            {
                Skip = "Set GEARANTEE_TEST_SQLSERVER=1 on Windows with LocalDB to run isolated SQL Server tests.";
            }
        }
    }
}
