using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Reservations;
using ASI.Basecode.Tests.UserAdministration;
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
                            ReservationStart = ManilaClock.ToLocal(nowUtc).Date.AddDays(1),
                            ReservationEnd = ManilaClock.ToLocal(nowUtc).Date.AddDays(1),
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

        [SqlServerFact]
        public async Task Concurrent_registration_allocates_unique_category_numbers()
        {
            var name = "Gearantee_ItemNumbers_" + Guid.NewGuid().ToString("N");
            var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=30")
                .Options;
            try
            {
                await using var setup = new AsiBasecodeDBContext(options);
                await setup.Database.MigrateAsync();
                var seed = await SeedAsync(setup);
                using var loggerFactory = LoggerFactory.Create(builder => { });
                var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                async Task<long> Register()
                {
                    await gate.Task;
                    await using var db = new AsiBasecodeDBContext(options);
                    var unit = new UnitOfWork(db);
                    var service = new EquipmentItemService(unit, new CategoryService(unit, loggerFactory), new EquipmentItemRepository(unit), loggerFactory);
                    var result = await service.CreateAsync(seed.AdminId, new EquipmentItemFormViewModel {
                        CategoryId = seed.CategoryId, ItemName = "Asus laptop", Location = "Test store",
                        ConditionStatus = DomainValues.ReturnConditions.Good, ItemStatus = DomainValues.EquipmentStatuses.Available
                    });
                    Assert.True(result.Succeeded, result.Message);
                    return result.EquipmentId;
                }
                var first = Task.Run(Register);
                var second = Task.Run(Register);
                gate.SetResult(true);
                var ids = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(60));
                var codes = await setup.EquipmentItems.AsNoTracking().Where(item => ids.Contains(item.EquipmentId))
                    .Select(item => item.ItemCode).ToListAsync();
                Assert.Equal(2, codes.Distinct().Count());
                Assert.Contains(codes, code => code.EndsWith("-001-ASU"));
                Assert.Contains(codes, code => code.EndsWith("-002-ASU"));
            }
            finally
            {
                await using var cleanup = new AsiBasecodeDBContext(options);
                await cleanup.Database.EnsureDeletedAsync();
            }
        }

        [SqlServerFact]
        public async Task Concurrent_registration_in_different_categories_retries_deadlock()
        {
            var name = "Gearantee_CrossCategory_" + Guid.NewGuid().ToString("N");
            var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=30";
            var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(connection).Options;
            try
            {
                await using var setup = new AsiBasecodeDBContext(options);
                await setup.Database.MigrateAsync();
                var seed = await SeedAsync(setup);
                var other = new EquipmentCategory { CategoryCode = "OTHER", CategoryName = "Other equipment", IsActive = true, NextItemNumber = 2 };
                setup.EquipmentCategories.Add(other);
                (await setup.EquipmentCategories.FindAsync(seed.CategoryId)).NextItemNumber = 2;
                await setup.SaveChangesAsync();
                var barrier = new RegistrationSaveBarrier();
                using var logger = LoggerFactory.Create(builder => { });
                async Task<long> Register(long categoryId)
                {
                    var concurrentOptions = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                        .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3, TimeSpan.FromMilliseconds(10), null))
                        .AddInterceptors(barrier).Options;
                    await using var db = new AsiBasecodeDBContext(concurrentOptions);
                    var unit = new UnitOfWork(db);
                    var service = new EquipmentItemService(unit, new CategoryService(unit, logger), new EquipmentItemRepository(unit), logger);
                    var result = await service.CreateAsync(seed.AdminId, Registration(categoryId));
                    Assert.True(result.Succeeded, result.Message);
                    return result.EquipmentId;
                }

                var ids = await Task.WhenAll(Task.Run(() => Register(seed.CategoryId)), Task.Run(() => Register(other.CategoryId)))
                    .WaitAsync(TimeSpan.FromSeconds(60));
                Assert.True(barrier.SaveAttempts >= 3, "The overlapping writes must exercise a real SQL Server deadlock retry.");
                var items = await setup.EquipmentItems.AsNoTracking().Where(item => ids.Contains(item.EquipmentId)).ToListAsync();
                Assert.Equal(2, items.Count);
                Assert.Equal(2, items.Select(item => item.ItemCode).Distinct().Count());
                Assert.All(items, item => Assert.EndsWith("-002-ASU", item.ItemCode));
                Assert.All(await setup.EquipmentCategories.AsNoTracking().ToListAsync(), category => Assert.Equal(3, category.NextItemNumber));
            }
            finally
            {
                await using var cleanup = new AsiBasecodeDBContext(options);
                await cleanup.Database.EnsureDeletedAsync();
            }
        }

        [SqlServerTheory]
        [InlineData(false, null)]
        [InlineData(true, null)]
        [InlineData(false, "SERIAL-RETRY")]
        [InlineData(true, "SERIAL-RETRY")]
        public async Task Registration_commit_failure_saves_exactly_once(bool afterCommit, string serialNumber)
        {
            var name = "Gearantee_RegistrationRetry_" + Guid.NewGuid().ToString("N");
            var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=30";
            var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(connection).Options;
            try
            {
                await using var setup = new AsiBasecodeDBContext(options);
                await setup.Database.MigrateAsync();
                var seed = await SeedAsync(setup);
                var failure = new RegistrationCommitFailure(afterCommit);
                var retryOptions = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3, TimeSpan.FromMilliseconds(10), null))
                    .AddInterceptors(failure).Options;
                await using var db = new AsiBasecodeDBContext(retryOptions);
                using var logger = LoggerFactory.Create(builder => { });
                var unit = new UnitOfWork(db);
                var service = new EquipmentItemService(unit, new CategoryService(unit, logger), new EquipmentItemRepository(unit), logger);
                var model = Registration(seed.CategoryId);
                model.SerialNumber = serialNumber;

                var result = await service.CreateAsync(seed.AdminId, model);

                Assert.True(failure.Fired);
                Assert.True(result.Succeeded, result.Message);
                var saved = Assert.Single(await setup.EquipmentItems.AsNoTracking().Where(item => item.ItemName == model.ItemName).ToListAsync());
                Assert.Equal(saved.EquipmentId, result.EquipmentId);
                Assert.EndsWith("-001-ASU", saved.ItemCode);
                Assert.Equal(serialNumber, saved.SerialNumber);
                Assert.Equal(2, await setup.EquipmentCategories.AsNoTracking().Where(category => category.CategoryId == seed.CategoryId).Select(category => category.NextItemNumber).SingleAsync());
            }
            finally
            {
                await using var cleanup = new AsiBasecodeDBContext(options);
                await cleanup.Database.EnsureDeletedAsync();
            }
        }

        private static EquipmentItemFormViewModel Registration(long categoryId) => new()
        {
            CategoryId = categoryId, ItemName = "Asus laptop", Location = "Test store",
            ConditionStatus = DomainValues.ReturnConditions.Good, ItemStatus = DomainValues.EquipmentStatuses.Available
        };

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

    internal sealed class RegistrationSaveBarrier : SaveChangesInterceptor
    {
        private int _attempts;
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SaveAttempts => _attempts;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _attempts) >= 2) _gate.TrySetResult(true);
            await _gate.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }

    internal sealed class RegistrationCommitFailure(bool afterCommit) : DbTransactionInterceptor
    {
        public bool Fired { get; private set; }
        private void FailOnce()
        {
            if (Fired) return;
            Fired = true;
            throw new TimeoutException("Simulated lost database connection at commit.");
        }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!afterCommit) FailOnce();
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (afterCommit) FailOnce();
            return Task.CompletedTask;
        }
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
