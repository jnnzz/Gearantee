using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.Tests.UserAdministration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Reservations
{
    /// <summary>
    /// Opt-in SQL Server tests for reservation-review permission revocation races.
    /// Run with GEARANTEE_TEST_SQLSERVER=1 on Windows with LocalDB.
    /// </summary>
    public sealed class ReservationReviewConcurrencyTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 17, 0);
        private const string ReviewPermission = DomainValues.Permissions.ReservationReview;
        private const string ReleasePermission = DomainValues.Permissions.TransactionReleaseReturn;

        [SqlServerFact]
        public Task SqlServer_approval_is_denied_after_review_permission_revocation() =>
            DeniedAfterRevocation(ActionKind.Approve);

        [SqlServerFact]
        public Task SqlServer_rejection_is_denied_after_review_permission_revocation() =>
            DeniedAfterRevocation(ActionKind.Reject);

        [SqlServerFact]
        public Task SqlServer_release_is_denied_after_release_permission_revocation() =>
            DeniedAfterRevocation(ActionKind.Release);

        [SqlServerFact]
        public Task SqlServer_review_permission_revocation_waits_for_in_flight_approval() =>
            InFlightWriteSerializesWithRevocation(ActionKind.Approve);

        [SqlServerFact]
        public Task SqlServer_review_permission_revocation_waits_for_in_flight_rejection() =>
            InFlightWriteSerializesWithRevocation(ActionKind.Reject);

        [SqlServerFact]
        public Task SqlServer_release_permission_revocation_waits_for_in_flight_release() =>
            InFlightWriteSerializesWithRevocation(ActionKind.Release);

        private static Task DeniedAfterRevocation(ActionKind action)
        {
            using var database = ReviewDatabase.Create();
            var scenario = Seed(database, action);

            using (var admin = database.NewContext())
            using (var transaction = admin.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                TakeExclusiveAdministrationLock(admin);
                RevokePermission(admin, PermissionFor(action));
                transaction.Commit();
            }

            var result = Execute(database, scenario, action);
            Assert.True(result.Forbidden, result.Message);
            AssertNoMutation(database, scenario, action);
            return Task.CompletedTask;
        }

        private static async Task InFlightWriteSerializesWithRevocation(ActionKind action)
        {
            using var database = ReviewDatabase.Create();
            var scenario = Seed(database, action);
            using var gate = new BeforeProtectedWrite(action);

            var writeTask = Task.Run(() => Execute(database, scenario, action, gate));
            Assert.True(gate.Reached.Wait(TimeSpan.FromSeconds(30)),
                $"The {action} write did not reach its protected database write.");

            var revokeTask = Task.Run(() =>
            {
                using var admin = database.NewContext();
                using var transaction = admin.Database.BeginTransaction(IsolationLevel.Serializable);
                TakeExclusiveAdministrationLock(admin);
                RevokePermission(admin, PermissionFor(action));
                transaction.Commit();
            });

            await Task.Delay(500);
            Assert.False(revokeTask.IsCompleted,
                "Permission revocation must wait while the authorized write holds the shared administration lock.");

            gate.Allow.Set();
            var result = await writeTask;
            Assert.True(result.Succeeded, result.Message);
            await revokeTask;
            AssertMutationCommitted(database, scenario, action);
        }

        private static Scenario Seed(ReviewDatabase database, ActionKind action)
        {
            using var db = database.NewContext();
            var borrower = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, borrower);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var initialStatus = action == ActionKind.Release
                ? DomainValues.ReservationStatuses.Approved
                : DomainValues.ReservationStatuses.Pending;
            var reservation = DashboardSeed.Reservation(
                db, profile, item, Now.AddDays(1), Now.AddDays(2), initialStatus);
            var custodian = DashboardSeed.User(db, "CUS-1");

            var role = db.Roles.SingleOrDefault(entry => entry.Name == DomainValues.Roles.Custodian);
            if (role == null)
            {
                role = new IdentityRole(DomainValues.Roles.Custodian);
                db.Roles.Add(role);
                db.SaveChanges();
            }

            db.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = custodian.Id,
                RoleId = role.Id
            });

            Grant(db, role.Id, ReviewPermission);
            if (action == ActionKind.Release)
            {
                Grant(db, role.Id, ReleasePermission);
            }

            db.SaveChanges();
            return new Scenario(custodian.Id, reservation.ReservationId, item.EquipmentId, borrower.Id, initialStatus);
        }

        private static void Grant(AsiBasecodeDBContext db, string roleId, string permissionName)
        {
            var permission = db.Permissions.SingleOrDefault(entry => entry.PermissionName == permissionName);
            if (permission == null)
            {
                permission = new Permission { PermissionName = permissionName, Description = permissionName };
                db.Permissions.Add(permission);
                db.SaveChanges();
            }

            if (!db.RolePermissions.Any(link => link.RoleId == roleId && link.PermissionId == permission.PermissionId))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.PermissionId });
                db.SaveChanges();
            }
        }

        private static void RevokePermission(AsiBasecodeDBContext db, string permissionName)
        {
            var role = db.Roles.Single(entry => entry.Name == DomainValues.Roles.Custodian);
            var permission = db.Permissions.Single(entry => entry.PermissionName == permissionName);
            db.RolePermissions
                .Where(link => link.RoleId == role.Id && link.PermissionId == permission.PermissionId)
                .ExecuteDelete();
        }

        private static void TakeExclusiveAdministrationLock(AsiBasecodeDBContext db)
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

        private static ReservationResult Execute(
            ReviewDatabase database,
            Scenario scenario,
            ActionKind action,
            params IInterceptor[] interceptors)
        {
            using var db = database.NewContext(interceptors);
            var service = ReservationTestHelpers.ReservationsFor(db, Now);
            return action switch
            {
                ActionKind.Approve => service.ApproveReservation(scenario.ActorId, scenario.ReservationId),
                ActionKind.Reject => service.RejectReservation(scenario.ActorId, scenario.ReservationId, "Test rejection reason"),
                ActionKind.Release => service.ReleaseEquipment(scenario.ActorId, scenario.ReservationId, "Test release notes"),
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
        }

        private static void AssertNoMutation(ReviewDatabase database, Scenario scenario, ActionKind action)
        {
            using var db = database.NewContext();
            Assert.Equal(scenario.InitialStatus, db.Reservations.Single(item => item.ReservationId == scenario.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
            Assert.Empty(db.ReleaseRecords);
            Assert.Equal(DomainValues.EquipmentStatuses.Available,
                db.EquipmentItems.Single(item => item.EquipmentId == scenario.EquipmentId).ItemStatus);
        }

        private static void AssertMutationCommitted(ReviewDatabase database, Scenario scenario, ActionKind action)
        {
            using var db = database.NewContext();
            var reservation = db.Reservations.Single(item => item.ReservationId == scenario.ReservationId);
            Assert.Single(db.AdministrationAuditEvents);
            if (action == ActionKind.Approve)
            {
                Assert.Equal(DomainValues.ReservationStatuses.Approved, reservation.Status);
                Assert.Empty(db.ReleaseRecords);
            }
            else if (action == ActionKind.Reject)
            {
                Assert.Equal(DomainValues.ReservationStatuses.Rejected, reservation.Status);
                Assert.Empty(db.ReleaseRecords);
            }
            else
            {
                Assert.Equal(DomainValues.ReservationStatuses.Approved, reservation.Status);
                Assert.Single(db.ReleaseRecords);
                Assert.Equal(DomainValues.EquipmentStatuses.Borrowed,
                    db.EquipmentItems.Single(item => item.EquipmentId == scenario.EquipmentId).ItemStatus);
            }
        }

        private static string PermissionFor(ActionKind action) =>
            action == ActionKind.Release ? ReleasePermission : ReviewPermission;

        private static DateTime Utc(int year, int month, int day, int hour) =>
            new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

        private enum ActionKind { Approve, Reject, Release }

        private sealed class Scenario
        {
            public Scenario(string actorId, long reservationId, long equipmentId, string borrowerId, string initialStatus)
            {
                ActorId = actorId;
                ReservationId = reservationId;
                EquipmentId = equipmentId;
                BorrowerId = borrowerId;
                InitialStatus = initialStatus;
            }

            public string ActorId { get; }
            public long ReservationId { get; }
            public long EquipmentId { get; }
            public string BorrowerId { get; }
            public string InitialStatus { get; }
        }

        private sealed class BeforeProtectedWrite : DbCommandInterceptor, IDisposable
        {
            private readonly ActionKind _action;
            private int _fired;

            public BeforeProtectedWrite(ActionKind action) => _action = action;

            public ManualResetEventSlim Reached { get; } = new();
            public ManualResetEventSlim Allow { get; } = new();

            public override InterceptionResult<int> NonQueryExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result)
            {
                PauseIfTarget(command);
                return result;
            }

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result)
            {
                PauseIfTarget(command);
                return result;
            }

            private void PauseIfTarget(DbCommand command)
            {
                var sql = command.CommandText;

                var isUpdate = sql.Contains(
                    "UPDATE",
                    StringComparison.OrdinalIgnoreCase);

                // Match the rejection write by the column it changes rather than
                // relying on SQL Server rendering the table name as "Reservations".
                var target = _action switch
                {
                    ActionKind.Release =>
                        sql.Contains(
                            "INSERT INTO [ReleaseRecord]",
                            StringComparison.OrdinalIgnoreCase),

                    ActionKind.Reject =>
                        isUpdate &&
                        sql.Contains(
                            "RejectionReason",
                            StringComparison.OrdinalIgnoreCase),

                    ActionKind.Approve =>
                        isUpdate &&
                        sql.Contains(
                            "Reservation",
                            StringComparison.OrdinalIgnoreCase),

                    _ => false
                };

                if (target && Interlocked.Exchange(ref _fired, 1) == 0)
                {
                    Reached.Set();

                    if (!Allow.Wait(TimeSpan.FromSeconds(30)))
                    {
                        throw new TimeoutException(
                            "Test did not release the intercepted reservation write.");
                    }
                }
            }

            public void Dispose()
            {
                Reached.Dispose();
                Allow.Dispose();
            }
        }

        private sealed class ReviewDatabase : IDisposable
        {
            private readonly string _connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=Gearantee_ReviewRace_Test_" +
                Guid.NewGuid().ToString("N") + ";Trusted_Connection=True;TrustServerCertificate=True";

            public static ReviewDatabase Create()
            {
                var database = new ReviewDatabase();
                using var db = database.NewContext();
                db.Database.Migrate();
                return database;
            }

            public AsiBasecodeDBContext NewContext(params IInterceptor[] interceptors) =>
                new(new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                    .UseSqlServer(_connectionString)
                    .AddInterceptors(interceptors)
                    .Options);

            public void Dispose()
            {
                using var db = NewContext();
                db.Database.EnsureDeleted();
            }
        }
    }
}