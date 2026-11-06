using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.ServiceModels.Returns;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.Tests.UserAdministration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using N = ASI.Basecode.Resources.Constants.DomainValues.NotificationTypes;

namespace ASI.Basecode.Tests.Notifications
{
    public class NotificationTests
    {
        [SqlServerFact]
        public async Task SqlServer_overlapping_workers_claim_email_once_and_reminders_deduplicate()
        {
            using var env = new Environment(sqlServer: true);
            env.Stage(N.Approved);
            using var secondDb = env.NewSqlContext();
            var sender = new Sender();
            var otherUnit = new UnitOfWork(secondDb);
            var firstEmail = new NotificationEmailProcessor(env.Unit, sender, env.Options, env.Clock);
            var secondEmail = new NotificationEmailProcessor(otherUnit, sender, env.Options, env.Clock);
            await Task.WhenAll(firstEmail.ProcessAsync(), secondEmail.ProcessAsync());
            Assert.Equal(1, sender.Calls);
            Assert.Equal("Sent", env.Db.Notifications.AsNoTracking().Single().EmailStatus);

            env.Reservation.Status = "Approved";
            env.Reservation.ReservationStart = env.Clock.Now.AddDays(-2);
            env.Reservation.ReservationEnd = env.Clock.Now.AddHours(-1);
            env.Db.SaveChanges();
            DashboardSeed.Release(env.Db, env.Reservation, env.Clock.Now.AddDays(-1));
            var secondDispatcher = new NotificationDispatcher(otherUnit, new NotificationRepository(otherUnit), env.Options, env.Clock);
            await Task.WhenAll(
                new NotificationReminderService(env.Unit, env.Dispatcher, env.Options, env.Clock).ScanAsync(),
                new NotificationReminderService(otherUnit, secondDispatcher, env.Options, env.Clock).ScanAsync());
            Assert.Equal(2, env.Db.Notifications.Count(x => x.Type == N.Overdue));
        }

        [Theory]
        [InlineData(N.Submitted, 2, 0)]
        [InlineData(N.Approved, 1, 1)]
        [InlineData(N.Rejected, 1, 1)]
        [InlineData(N.Cancelled, 2, 0)]
        [InlineData(N.Expired, 2, 0)]
        [InlineData(N.Released, 1, 0)]
        [InlineData(N.Returned, 1, 0)]
        [InlineData(N.LateReturn, 2, 0)]
        [InlineData(N.Overdue, 2, 0)]
        [InlineData(N.DueSoon, 1, 0)]
        public void Matrix_targets_only_affected_active_accounts_and_deduplicates(string type, int count, int emails)
        {
            using var env = new Environment();
            env.Stage(type, "Reason <script>alert(1)</script>");
            env.Stage(type);
            Assert.Equal(count, env.Db.Notifications.Count());
            Assert.Equal(emails, env.Db.Notifications.Count(x => x.EmailRequested));
            Assert.DoesNotContain(env.Db.Notifications, x => x.UserId == "other" || x.UserId == "inactive");
            if (emails > 0)
            {
                var row = Assert.Single(env.Db.Notifications.Where(x => x.EmailRequested));
                Assert.Equal(env.Borrower.Id, row.UserId);
                Assert.Contains("<script>", row.Body); // Razor/email rendering, not storage, encodes text.
            }
        }

        [Fact]
        public void Notifications_rollback_with_the_domain_transaction()
        {
            using var env = new Environment();
            using (var transaction = env.Db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                env.Dispatcher.StageReservation(env.Reservation.ReservationId, N.Approved);
                env.Db.SaveChanges();
                transaction.Rollback();
            }
            env.Db.ChangeTracker.Clear();
            Assert.Empty(env.Db.Notifications);
        }

        [Fact]
        public void Submit_approve_release_and_repeated_decision_produce_only_committed_events()
        {
            using var env = new Environment();
            var item = DashboardSeed.Item(env.Db, "SECOND");
            var result = env.Service.SubmitReservation(new ReservationCreateViewModel
            {
                EquipmentId = item.EquipmentId, ReservationStart = env.Clock.Now.AddDays(3).Date,
                ReservationEnd = env.Clock.Now.AddDays(4).Date, Purpose = "Notification test"
            }, env.Borrower.Id);
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(2, env.Db.Notifications.Count(x => x.Type == N.Submitted));
            Assert.True(env.Service.ApproveReservation("custodian", env.Reservation.ReservationId).Succeeded);
            Assert.False(env.Service.ApproveReservation("custodian", env.Reservation.ReservationId).Succeeded);
            Assert.Single(env.Db.Notifications.Where(x => x.Type == N.Approved));
            Assert.True(env.Service.ReleaseEquipment("custodian", env.Reservation.ReservationId, null).Succeeded);
            Assert.Single(env.Db.Notifications.Where(x => x.Type == N.Released));
        }

        [Fact]
        public void Cancel_expire_and_reject_hook_direct_updates_without_duplicates()
        {
            using var env = new Environment();
            Assert.True(env.Service.CancelReservation(env.Reservation.ReservationId, env.Borrower.Id).Succeeded);
            Assert.False(env.Service.CancelReservation(env.Reservation.ReservationId, env.Borrower.Id).Succeeded);
            Assert.Equal(2, env.Db.Notifications.Count(x => x.Type == N.Cancelled));
            var stale = DashboardSeed.Reservation(env.Db, env.Profile, env.Reservation.EquipmentItem,
                env.Clock.Now.AddHours(-1), env.Clock.Now.AddHours(1), "Pending");
            env.Service.GetReviewQueue(null, null, 1);
            env.Service.GetReviewQueue(null, null, 1);
            Assert.Equal(2, env.Db.Notifications.Count(x => x.Type == N.Expired));
            var pending = DashboardSeed.Reservation(env.Db, env.Profile, env.Reservation.EquipmentItem,
                env.Clock.Now.AddDays(3), env.Clock.Now.AddDays(4), "Pending");
            Assert.True(env.Service.RejectReservation("custodian", pending.ReservationId, "Required for class").Succeeded);
            Assert.Contains("Required for class", env.Db.Notifications.Single(x => x.Type == N.Rejected).Body);
        }

        [Fact]
        public void Late_return_commits_one_borrower_notice_and_one_admin_notice()
        {
            using var env = new Environment();
            env.Reservation.ReservationStart = env.Clock.Now.AddDays(-3);
            env.Reservation.ReservationEnd = env.Clock.Now.AddDays(-1);
            env.Reservation.Status = "Approved";
            env.Db.SaveChanges();
            var release = DashboardSeed.Release(env.Db, env.Reservation, env.Clock.Now.AddDays(-2));
            var service = new ReturnRecordService(env.Unit, new ReturnRecordRepository(env.Unit),
                new ReservationRepository(env.Unit), new EquipmentItemRepository(env.Unit), env.Clock, env.Dispatcher);
            var form = new ReturnRecordViewModel { ReleaseRecordId = release.ReleaseRecordId,
                ActualReturnAt = ManilaClock.ToLocal(env.Clock.Now), ReturnedCondition = "Good", ResultingItemStatus = "Available" };
            var result = service.AddReturnRecord(form, "custodian");
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(2, env.Db.Notifications.Count(x => x.Type == N.LateReturn));
            Assert.Single(env.Db.Notifications.Where(x => x.UserId == env.Borrower.Id));
            Assert.Contains("Condition: Good", env.Db.Notifications.First().Body);
            Assert.False(service.AddReturnRecord(form, "custodian").Succeeded);
            Assert.Equal(2, env.Db.Notifications.Count());
        }

        [Fact]
        public void Notification_failure_rolls_back_the_actual_approval()
        {
            using var env = new Environment();
            var service = new ReservationService(env.Unit, new ReservationRepository(env.Unit),
                new EquipmentItemRepository(env.Unit), new BorrowerProfileRepository(env.Unit),
                new AuditService(env.Unit), env.Clock, new BrokenDispatcher());
            Assert.Throws<InvalidOperationException>(() => service.ApproveReservation("custodian", env.Reservation.ReservationId));
            env.Db.ChangeTracker.Clear();
            Assert.Equal("Pending", env.Db.Reservations.Single().Status);
            Assert.Empty(env.Db.Notifications);
            Assert.Empty(env.Db.AdministrationAuditEvents);
        }

        [Fact]
        public void Disabled_notifications_and_revoked_staff_grants_do_not_create_rows()
        {
            using var env = new Environment();
            env.Options.Value.Enabled = false;
            env.Stage(N.Approved);
            Assert.Empty(env.Db.Notifications);
            env.Options.Value.Enabled = true;
            env.Db.RolePermissions.ExecuteDelete();
            env.Stage(N.Submitted);
            Assert.Empty(env.Db.Notifications);
        }

        [Fact]
        public async Task Inbox_paginates_ten_and_scopes_reads_to_current_user()
        {
            using var env = new Environment();
            for (var i = 0; i < 21; i++) env.Db.Notifications.Add(new Notification
            {
                UserId = env.Borrower.Id, Type = N.Approved, Title = "Approved", Body = "test",
                DedupKey = "test-" + i, CreatedAt = env.Clock.Now
            });
            env.Db.SaveChanges();
            var service = new NotificationService(env.Repository, env.Clock);
            Assert.Equal(10, (await service.GetPageAsync(env.Borrower.Id, 1)).Items.Count);
            Assert.Single((await service.GetPageAsync(env.Borrower.Id, int.MaxValue)).Items);
            var id = env.Db.Notifications.Select(x => x.NotificationId).First();
            await service.MarkReadAsync("other", id);
            Assert.Equal(21, await service.UnreadCountAsync(env.Borrower.Id));
            Assert.Empty((await service.GetPageAsync("other", 1)).Items);
            await service.MarkReadAsync(env.Borrower.Id, id);
            Assert.Equal(20, await service.UnreadCountAsync(env.Borrower.Id));
            await service.MarkReadAsync(env.Borrower.Id, null);
            Assert.Equal(0, await service.UnreadCountAsync(env.Borrower.Id));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Reminders_respect_due_boundary_deduplicate_and_stop_after_return(bool fullDay)
        {
            using var env = new Environment();
            env.Reservation.Status = "Approved";
            env.Reservation.IsFullDay = fullDay;
            env.Reservation.ReservationStart = env.Clock.Now.AddDays(-1);
            env.Reservation.ReservationEnd = env.Clock.Now.AddHours(24);
            env.Db.SaveChanges();
            var release = DashboardSeed.Release(env.Db, env.Reservation, env.Clock.Now.AddHours(-1));
            var reminders = new NotificationReminderService(env.Unit, env.Dispatcher, env.Options, env.Clock);
            env.Clock.Now = env.Clock.Now.AddSeconds(-1);
            await reminders.ScanAsync();
            Assert.Empty(env.Db.Notifications);
            env.Clock.Now = env.Clock.Now.AddSeconds(1);
            await reminders.ScanAsync();
            await reminders.ScanAsync();
            Assert.Single(env.Db.Notifications.Where(x => x.Type == N.DueSoon));
            env.Clock.Now = env.Reservation.ReservationEnd;
            await reminders.ScanAsync();
            Assert.Empty(env.Db.Notifications.Where(x => x.Type == N.Overdue));
            env.Clock.Now = env.Clock.Now.AddSeconds(1);
            await reminders.ScanAsync();
            await reminders.ScanAsync();
            Assert.Equal(2, env.Db.Notifications.Count(x => x.Type == N.Overdue));
            env.Db.ReturnRecords.Add(new ReturnRecord { ReleaseRecordId = release.ReleaseRecordId,
                ReceivedByUserId = "custodian", ActualReturnAt = env.Clock.Now, ReturnedCondition = "Good", ResultingItemStatus = "Available" });
            env.Db.SaveChanges();
            env.Db.Notifications.ExecuteDelete();
            await reminders.ScanAsync();
            Assert.Empty(env.Db.Notifications);
        }

        [Fact]
        public async Task Email_retries_are_bounded_and_do_not_change_reservations()
        {
            using var env = new Environment();
            env.Stage(N.Rejected);
            var sender = new Sender { Fail = true };
            var processor = new NotificationEmailProcessor(env.Unit, sender, env.Options, env.Clock);
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                await processor.ProcessAsync();
                var row = env.Db.Notifications.AsNoTracking().Single();
                Assert.Equal(attempt, row.EmailAttempts);
                await processor.ProcessAsync();
                Assert.Equal(attempt, sender.Calls);
                env.Clock.Now = env.Clock.Now.AddHours(1);
            }
            Assert.Equal("Failed", env.Db.Notifications.AsNoTracking().Single().EmailStatus);
            Assert.Equal("Pending", env.Db.Reservations.AsNoTracking().Single().Status);
        }

        [Fact]
        public async Task Email_claim_blocks_second_worker_and_encodes_content()
        {
            using var env = new Environment();
            env.Stage(N.Rejected, "<img src=x onerror=alert(1)>");
            var sender = new Sender();
            var processor = new NotificationEmailProcessor(env.Unit, sender, env.Options, env.Clock);
            sender.DuringSend = () => new NotificationEmailProcessor(env.Unit, sender, env.Options, env.Clock).ProcessAsync();
            await processor.ProcessAsync();
            Assert.Equal(1, sender.Calls);
            Assert.Contains("&lt;img", sender.Html);
            Assert.DoesNotContain("<img", sender.Html);
            Assert.Equal("Sent", env.Db.Notifications.AsNoTracking().Single().EmailStatus);
        }

        [Fact]
        public async Task Expired_lease_recovers_and_inactive_recipient_is_not_emailed()
        {
            using var env = new Environment();
            env.Stage(N.Approved);
            var row = env.Db.Notifications.Single();
            row.EmailLeaseToken = "abandoned";
            row.EmailLeaseExpiresAt = env.Clock.Now.AddMinutes(-1);
            env.Borrower.IsActive = false;
            env.Db.SaveChanges();
            var sender = new Sender();
            await new NotificationEmailProcessor(env.Unit, sender, env.Options, env.Clock).ProcessAsync();
            Assert.Equal(0, sender.Calls);
            Assert.Equal("Failed", env.Db.Notifications.AsNoTracking().Single().EmailStatus);
        }

        private sealed class Sender : IBrevoEmailSender
        {
            public int Calls; public bool Fail; public string Html; public Func<Task> DuringSend;
            public async Task SendAsync(string email, string name, string subject, string html, CancellationToken cancellationToken = default)
            { Interlocked.Increment(ref Calls); Html = html; if (DuringSend != null) await DuringSend(); if (Fail) throw new InvalidOperationException("Test failure"); }
            public Task SendPasswordResetOtpAsync(string email, string name, string otp, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
        private sealed class BrokenDispatcher : INotificationDispatcher
        {
            public void StageReservation(long id, string type, string detail = null) => throw new InvalidOperationException("Storage failure");
        }
        private sealed class Clock : TimeProvider
        {
            public DateTime Now = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
            public override DateTimeOffset GetUtcNow() => new(Now);
        }
        private sealed class Environment : IDisposable
        {
            private readonly SqliteDb _fixture;
            private readonly string _sqlConnection;
            public AsiBasecodeDBContext Db;
            public UnitOfWork Unit;
            public NotificationRepository Repository;
            public NotificationDispatcher Dispatcher;
            public ReservationService Service;
            public ApplicationUser Borrower;
            public BorrowerProfile Profile;
            public Reservation Reservation;
            public Clock Clock = new();
            public IOptions<NotificationOptions> Options = Microsoft.Extensions.Options.Options.Create(new NotificationOptions { Enabled = true });
            public Environment(bool sqlServer = false)
            {
                if (sqlServer)
                {
                    _sqlConnection = @"Server=(localdb)\MSSQLLocalDB;Database=Gearantee_Notifications_Test_" + Guid.NewGuid().ToString("N") + ";Integrated Security=True;TrustServerCertificate=True";
                    Db = NewSqlContext();
                    Db.Database.Migrate();
                }
                else { _fixture = new SqliteDb(); Db = _fixture.Create(); }
                Unit = new UnitOfWork(Db); Repository = new NotificationRepository(Unit);
                Dispatcher = new NotificationDispatcher(Unit, Repository, Options, Clock);
                Borrower = DashboardSeed.User(Db, "BORROWER");
                DashboardSeed.User(Db, "OTHER");
                Profile = DashboardSeed.Profile(Db, Borrower);
                var item = DashboardSeed.Item(Db, "ITEM");
                Reservation = DashboardSeed.Reservation(Db, Profile, item, Clock.Now.AddDays(1), Clock.Now.AddDays(2), "Pending");
                Grant("CUSTODIAN", "Custodian", true, DomainValues.Permissions.ReservationReview, DomainValues.Permissions.TransactionReleaseReturn);
                Grant("ADMIN", "Administrator", true, DomainValues.Permissions.HistoryView);
                Grant("INACTIVE", "Custodian", false);
                Service = new ReservationService(Unit, new ReservationRepository(Unit), new EquipmentItemRepository(Unit),
                    new BorrowerProfileRepository(Unit), new AuditService(Unit), Clock, Dispatcher);
            }
            private void Grant(string code, string roleName, bool active, params string[] permissions)
            {
                var user = DashboardSeed.User(Db, code, active);
                var role = Db.Roles.SingleOrDefault(x => x.Name == roleName) ?? new IdentityRole(roleName) { NormalizedName = roleName.ToUpperInvariant() };
                if (Db.Entry(role).State == EntityState.Detached) Db.Roles.Add(role);
                Db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
                foreach (var name in permissions) Db.RolePermissions.Add(new RolePermission { Role = role,
                    Permission = new Permission { PermissionName = name, Description = name } });
                Db.SaveChanges();
            }
            public void Stage(string type, string detail = null)
            {
                Db.Database.CreateExecutionStrategy().Execute(() =>
                {
                    using var transaction = Db.Database.BeginTransaction(IsolationLevel.Serializable);
                    Dispatcher.StageReservation(Reservation.ReservationId, type, detail);
                    Db.SaveChanges(); transaction.Commit();
                });
            }
            public AsiBasecodeDBContext NewSqlContext() => new(new DbContextOptionsBuilder<AsiBasecodeDBContext>()
                .UseSqlServer(_sqlConnection, sql => sql.EnableRetryOnFailure()).Options);
            public void Dispose()
            {
                if (_sqlConnection != null) Db.Database.EnsureDeleted();
                Db.Dispose(); _fixture?.Dispose();
            }
        }
    }
}
