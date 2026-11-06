using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Returns;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.Tests.UserAdministration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;
using static ASI.Basecode.Tests.Returns.ReturnTestHelpers;

namespace ASI.Basecode.Tests.Returns
{
    /// <summary>Opt-in SQL Server races for returns; set GEARANTEE_TEST_SQLSERVER=1.</summary>
    public sealed class ReturnRecordConcurrencyTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 17, 4);

        [SqlServerFact]
        public async Task SqlServer_concurrent_returns_of_one_loan_save_one_record()
        {
            using var database = new ReturnDatabase();
            string custodianId;
            long releaseId;
            using (var db = database.NewContext())
            {
                custodianId = Staff(db, "CUS-1", DomainValues.Roles.Custodian).Id;
                releaseId = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2)).ReleaseRecordId;
            }

            var results = await Task.WhenAll(
                ReturnAsync(database, releaseId, custodianId),
                ReturnAsync(database, releaseId, custodianId));

            Assert.Single(results, result => result.Succeeded);
            Assert.Single(results, result => !result.Succeeded && result.Message.Contains("already been returned"));
            using var check = database.NewContext();
            Assert.Equal(1, check.ReturnRecords.Count());
        }

        [SqlServerFact]
        public async Task SqlServer_concurrent_returns_of_different_items_both_succeed()
        {
            using var database = new ReturnDatabase();
            string custodianId;
            long firstReleaseId, secondReleaseId;
            using (var db = database.NewContext())
            {
                custodianId = Staff(db, "CUS-1", DomainValues.Roles.Custodian).Id;
                firstReleaseId = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2)).ReleaseRecordId;
                secondReleaseId = Loan(db, "ITEM-2", Now.AddDays(-2), Now.AddDays(-1), Now.AddDays(-2)).ReleaseRecordId;
            }

            var results = await Task.WhenAll(
                ReturnAsync(database, firstReleaseId, custodianId),
                ReturnAsync(database, secondReleaseId, custodianId));

            Assert.All(results, result => Assert.True(result.Succeeded, result.Message));
            using var check = database.NewContext();
            Assert.Equal(2, check.ReturnRecords.Count());
            Assert.Equal(1, check.LateReturns.Count());
        }

        [SqlServerFact]
        public async Task SqlServer_return_waits_for_a_pending_revocation_then_is_refused()
        {
            using var database = new ReturnDatabase();
            string custodianId;
            long releaseId;
            using (var db = database.NewContext())
            {
                custodianId = Staff(db, "CUS-1", DomainValues.Roles.Custodian).Id;
                releaseId = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2)).ReleaseRecordId;
            }

            // Admin revocation holds the lock, uncommitted
            using var admin = database.NewContext();
            using var revocation = admin.Database.BeginTransaction(IsolationLevel.Serializable);
            TakeAdministrationLock(admin);
            RevokeCustodianGrant(admin);

            var pending = ReturnAsync(database, releaseId, custodianId, () => { });
            await Task.Delay(1000);
            Assert.False(pending.IsCompleted, "The return did not wait for the administration lock.");

            revocation.Commit();
            var result = await pending;

            Assert.False(result.Succeeded);
            Assert.Contains("permission", result.Message);
            using var check = database.NewContext();
            Assert.Empty(check.ReturnRecords);
            Assert.Equal(DomainValues.EquipmentStatuses.Borrowed, check.EquipmentItems.Single().ItemStatus);
        }

        [SqlServerFact]
        public async Task SqlServer_revocation_waits_until_an_in_flight_return_commits()
        {
            using var database = new ReturnDatabase();
            string custodianId;
            long releaseId;
            using (var db = database.NewContext())
            {
                custodianId = Staff(db, "CUS-1", DomainValues.Roles.Custodian).Id;
                releaseId = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2)).ReleaseRecordId;
            }

            // Return passes its permission check, then pauses before saving
            using var reachedSave = new ManualResetEventSlim();
            var pending = ReturnAsync(database, releaseId, custodianId, () =>
            {
                reachedSave.Set();
                Thread.Sleep(1500);
            });
            Assert.True(reachedSave.Wait(TimeSpan.FromSeconds(30)), "The return never reached its save.");

            int returnsSeenByRevocation;
            using (var admin = database.NewContext())
            using (var revocation = admin.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                TakeAdministrationLock(admin);
                returnsSeenByRevocation = admin.ReturnRecords.Count();
                RevokeCustodianGrant(admin);
                revocation.Commit();
            }

            var result = await pending;
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(1, returnsSeenByRevocation);
            using var check = database.NewContext();
            Assert.Empty(check.RolePermissions);
        }

        // Same protocol as UserAdministrationService: Exclusive app lock in a Serializable transaction
        private static void TakeAdministrationLock(AsiBasecodeDBContext db)
        {
            db.Database.ExecuteSqlInterpolated(
                $@"DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource = {AdministrationLock.ResourceName},
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 30000;
IF @lockResult < 0 THROW 51000, 'Could not acquire the user administration lock.', 1;");
        }

        private static void RevokeCustodianGrant(AsiBasecodeDBContext db)
        {
            var custodianRoleId = db.Roles.Single(role => role.Name == DomainValues.Roles.Custodian).Id;
            db.RolePermissions.Where(link => link.RoleId == custodianRoleId).ExecuteDelete();
        }

        // Holds each INSERT back so both requests pass their checks first
        private static Task<ReturnRecordResult> ReturnAsync(ReturnDatabase database, long releaseRecordId, string userId) =>
            ReturnAsync(database, releaseRecordId, userId, () => Thread.Sleep(500));

        private static Task<ReturnRecordResult> ReturnAsync(ReturnDatabase database, long releaseRecordId, string userId, Action beforeInsert)
        {
            return Task.Run(() =>
            {
                using var db = database.NewContext(new BeforeReturnInsert(beforeInsert));
                return ReturnsFor(db, Now).AddReturnRecord(new ReturnRecordViewModel
                {
                    ReleaseRecordId = releaseRecordId,
                    ActualReturnAt = ManilaClock.ToLocal(Now),
                    ReturnedCondition = DomainValues.ReturnConditions.Good,
                    ResultingItemStatus = DomainValues.EquipmentStatuses.Available
                }, userId);
            });
        }

        private sealed class BeforeReturnInsert : DbCommandInterceptor
        {
            private readonly Action _action;
            private bool _fired;

            public BeforeReturnInsert(Action action) => _action = action;

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            {
                if (!_fired && command.CommandText.Contains("INSERT INTO [ReturnRecord]", StringComparison.OrdinalIgnoreCase))
                {
                    _fired = true;
                    _action();
                }

                return result;
            }
        }

        // Uniquely named LocalDB database built from migrations, dropped afterwards
        private sealed class ReturnDatabase : IDisposable
        {
            private readonly string _connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=Gearantee_Returns_Test_" +
                Guid.NewGuid().ToString("N") + ";Trusted_Connection=True;TrustServerCertificate=True";

            public ReturnDatabase()
            {
                using var db = NewContext();
                db.Database.Migrate();
            }

            public AsiBasecodeDBContext NewContext(params IInterceptor[] interceptors)
            {
                return new AsiBasecodeDBContext(new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                    .UseSqlServer(_connectionString)
                    .AddInterceptors(interceptors)
                    .Options);
            }

            public void Dispose()
            {
                using var db = NewContext();
                db.Database.EnsureDeleted();
            }
        }
    }
}
