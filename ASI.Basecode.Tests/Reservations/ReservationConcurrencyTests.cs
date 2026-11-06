using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.Tests.UserAdministration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    /// <summary>
    /// Regression tests for PR #26 review findings RES-01 (duplicate Pending requests under
    /// concurrent submits) and RES-02 (expiry overwriting a concurrent custodian decision).
    /// The SQL Server cases are opt-in like the rest of the suite: set GEARANTEE_TEST_SQLSERVER=1.
    /// </summary>
    public sealed class ReservationConcurrencyTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 7, 0);

        [Fact]
        public void Expiry_does_not_overwrite_an_approval_that_lands_mid_request() =>
            ExpiryRacesApproval(sqlServer: false);

        [SqlServerFact]
        public void SqlServer_expiry_does_not_overwrite_an_approval_that_lands_mid_request() =>
            ExpiryRacesApproval(sqlServer: true);

        [SqlServerFact]
        public Task SqlServer_concurrent_identical_submissions_save_one_pending_request() =>
            ConcurrentSubmissions(secondStartOffset: TimeSpan.Zero, secondEndOffset: TimeSpan.Zero);

        [SqlServerFact]
        public Task SqlServer_concurrent_partially_overlapping_submissions_save_one_pending_request() =>
            ConcurrentSubmissions(secondStartOffset: TimeSpan.FromHours(1), secondEndOffset: TimeSpan.FromHours(2));

        [SqlServerFact]
        public async Task SqlServer_concurrent_submissions_for_different_items_both_succeed()
        {
            using var database = ReservationDatabase.Create(sqlServer: true);
            string userId;
            long firstItemId, secondItemId;
            using (var db = database.NewContext())
            {
                var user = DashboardSeed.User(db, "BRW-1");
                DashboardSeed.Profile(db, user);
                userId = user.Id;
                firstItemId = DashboardSeed.Item(db, "ITEM-1").EquipmentId;
                secondItemId = DashboardSeed.Item(db, "ITEM-2").EquipmentId;
            }

            var results = await Task.WhenAll(
                SubmitAsync(database, Request(firstItemId, Now.AddDays(1), Now.AddDays(1).AddHours(3)), userId),
                SubmitAsync(database, Request(secondItemId, Now.AddDays(1), Now.AddDays(1).AddHours(3)), userId));

            Assert.All(results, result => Assert.True(result.Succeeded, result.Message));
            using var check = database.NewContext();
            Assert.Equal(2, check.Reservations.Count(reservation => reservation.Status == DomainValues.ReservationStatuses.Pending));
        }

        /// <summary>
        /// The borrower opens My Reservations while a stale Pending row is waiting to be expired.
        /// Just before the expiry UPDATE reaches the database, a custodian approves that row on
        /// another connection. The approval must survive.
        /// </summary>
        private static void ExpiryRacesApproval(bool sqlServer)
        {
            using var database = ReservationDatabase.Create(sqlServer);
            string userId;
            long reservationId;
            using (var db = database.NewContext())
            {
                var user = DashboardSeed.User(db, "BRW-1");
                var profile = DashboardSeed.Profile(db, user);
                var item = DashboardSeed.Item(db, "ITEM-1");
                userId = user.Id;
                reservationId = DashboardSeed.Reservation(db, profile, item,
                    Now.AddHours(-1), Now.AddHours(2), DomainValues.ReservationStatuses.Pending, Now.AddDays(-1)).ReservationId;
            }

            var approval = new BeforeReservationWrite("UPDATE", () =>
            {
                using var custodian = database.NewContext();
                custodian.Reservations
                    .Where(reservation => reservation.ReservationId == reservationId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(reservation => reservation.Status, DomainValues.ReservationStatuses.Approved)
                        .SetProperty(reservation => reservation.ReviewedAt, Now));
            });

            using (var db = database.NewContext(approval))
            {
                var page = ReservationsFor(db, Now).GetMyReservations(userId, null, 1);
                Assert.Equal(DomainValues.ReservationStatuses.Approved, page.Items.Single().Status);
            }

            Assert.True(approval.Fired, "The approval was never injected, so the race was not exercised.");
            using var check = database.NewContext();
            var saved = check.Reservations.Single(reservation => reservation.ReservationId == reservationId);
            Assert.Equal(DomainValues.ReservationStatuses.Approved, saved.Status);
            Assert.Equal(Now, saved.ReviewedAt);
        }

        /// <summary>
        /// Two requests for the same borrower and item both pass every check before either one
        /// inserts: each INSERT is held back long enough that, without serialization, both
        /// duplicate checks would see an empty table.
        /// </summary>
        private static async Task ConcurrentSubmissions(TimeSpan secondStartOffset, TimeSpan secondEndOffset)
        {
            using var database = ReservationDatabase.Create(sqlServer: true);
            string userId;
            long itemId;
            using (var db = database.NewContext())
            {
                var user = DashboardSeed.User(db, "BRW-1");
                DashboardSeed.Profile(db, user);
                userId = user.Id;
                itemId = DashboardSeed.Item(db, "ITEM-1").EquipmentId;
            }

            var start = Now.AddDays(1);
            var end = start.AddHours(3);
            var results = await Task.WhenAll(
                SubmitAsync(database, Request(itemId, start, end), userId),
                SubmitAsync(database, Request(itemId, start + secondStartOffset, end + secondEndOffset), userId));

            Assert.Single(results, result => result.Succeeded);
            Assert.Single(results, result => !result.Succeeded && result.Message.Contains("already have a pending request"));
            using var check = database.NewContext();
            Assert.Equal(1, check.Reservations.Count(reservation => reservation.Status == DomainValues.ReservationStatuses.Pending));
        }

        private static Task<ReservationResult> SubmitAsync(ReservationDatabase database, ReservationCreateViewModel request, string userId)
        {
            return Task.Run(() =>
            {
                var delay = new BeforeReservationWrite("INSERT", () => Thread.Sleep(500));
                using var db = database.NewContext(delay);
                return ReservationsFor(db, Now).SubmitReservation(request, userId);
            });
        }

        private static ReservationCreateViewModel Request(long equipmentId, DateTime startUtc, DateTime endUtc) =>
            new ReservationCreateViewModel
            {
                EquipmentId = equipmentId,
                ReservationStart = ManilaClock.ToLocal(startUtc),
                ReservationEnd = ManilaClock.ToLocal(endUtc),
                Purpose = "Thesis defense"
            };

        /// <summary>Runs an action once, right before the first matching write to Reservations.</summary>
        private sealed class BeforeReservationWrite : DbCommandInterceptor
        {
            private readonly string _verb;
            private readonly Action _action;

            public BeforeReservationWrite(string verb, Action action)
            {
                _verb = verb;
                _action = action;
            }

            public bool Fired { get; private set; }

            public override InterceptionResult<int> NonQueryExecuting(
                DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
            {
                Run(command);
                return result;
            }

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            {
                Run(command);
                return result;
            }

            private void Run(DbCommand command)
            {
                var sql = command.CommandText;
                if (Fired ||
                    !sql.Contains("Reservations", StringComparison.OrdinalIgnoreCase) ||
                    !sql.Contains(_verb + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Fired = true;
                _action();
            }
        }

        /// <summary>
        /// One test database: the shared in-memory SQLite connection, or a uniquely named LocalDB
        /// database that is created from the migrations and dropped afterwards.
        /// </summary>
        private sealed class ReservationDatabase : IDisposable
        {
            private readonly SqliteDb _sqlite;
            private readonly string _sqlServer;

            private ReservationDatabase(bool sqlServer)
            {
                if (sqlServer)
                {
                    _sqlServer = @"Server=(localdb)\MSSQLLocalDB;Database=Gearantee_Pr26_Test_" +
                        Guid.NewGuid().ToString("N") + ";Trusted_Connection=True;TrustServerCertificate=True";
                }
                else
                {
                    _sqlite = new SqliteDb();
                }
            }

            public static ReservationDatabase Create(bool sqlServer)
            {
                var database = new ReservationDatabase(sqlServer);
                if (sqlServer)
                {
                    using var db = database.NewContext();
                    db.Database.Migrate();
                }

                return database;
            }

            public AsiBasecodeDBContext NewContext(params IInterceptor[] interceptors)
            {
                if (_sqlite != null)
                {
                    return new SqliteDashboardDbContext(new DbContextOptionsBuilder<SqliteDashboardDbContext>()
                        .UseSqlite(_sqlite.Connection)
                        .AddInterceptors(interceptors)
                        .Options);
                }

                return new AsiBasecodeDBContext(new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                    .UseSqlServer(_sqlServer)
                    .AddInterceptors(interceptors)
                    .Options);
            }

            public void Dispose()
            {
                if (_sqlServer != null)
                {
                    using var db = NewContext();
                    db.Database.EnsureDeleted();
                }

                _sqlite?.Dispose();
            }
        }
    }
}
