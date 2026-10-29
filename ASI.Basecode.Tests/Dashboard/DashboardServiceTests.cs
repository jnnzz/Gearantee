using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Dashboard;
using ASI.Basecode.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Dashboard
{
    public sealed class DashboardServiceTests
    {
        [Fact]
        public async Task Borrower_sees_only_own_reservations()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 9, 30, 8, 0);
            var userA = DashboardSeed.User(db, "BORROWER-A");
            var userB = DashboardSeed.User(db, "BORROWER-B");
            var profileA = DashboardSeed.Profile(db, userA);
            var profileB = DashboardSeed.Profile(db, userB);
            var itemA = DashboardSeed.Item(db, "ITEM-A", DomainValues.EquipmentStatuses.Borrowed);
            var itemB = DashboardSeed.Item(db, "ITEM-B", DomainValues.EquipmentStatuses.Borrowed);

            DashboardSeed.Reservation(db, profileA, itemA, now.AddHours(-1), now.AddHours(3), DomainValues.ReservationStatuses.Pending, now.AddMinutes(-4));
            var activeA = DashboardSeed.Reservation(db, profileA, itemA, now.AddHours(-2), now.AddHours(4), DomainValues.ReservationStatuses.Approved, now.AddMinutes(-3));
            DashboardSeed.Release(db, activeA, now.AddHours(-2));
            DashboardSeed.Reservation(db, profileB, itemB, now.AddHours(-1), now.AddHours(3), DomainValues.ReservationStatuses.Pending, now.AddMinutes(-2));
            var activeB = DashboardSeed.Reservation(db, profileB, itemB, now.AddHours(-2), now.AddHours(4), DomainValues.ReservationStatuses.Approved, now.AddMinutes(-1));
            DashboardSeed.Release(db, activeB, now.AddHours(-2));

            var service = Service(db, now);
            var model = await service.GetBorrowerDashboardAsync(userA.Id);

            Assert.Equal(1, model.PendingCount);
            Assert.Equal(1, model.ActiveLoanCount);
            Assert.All(model.RecentRequests, row => Assert.Equal(userA.UserCode, row.SchoolId));
            Assert.All(model.CurrentLoans, row => Assert.Equal(userA.UserCode, row.SchoolId));
        }

        [Fact]
        public async Task Borrower_without_profile_gets_zero_counts()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "NO-PROFILE");

            var model = await Service(db, Utc(2026, 9, 30, 8, 0))
                .GetBorrowerDashboardAsync(user.Id);

            Assert.False(model.HasProfile);
            Assert.Equal(0, model.PendingCount);
            Assert.Equal(0, model.ApprovedAwaitingPickupCount);
            Assert.Equal(0, model.ActiveLoanCount);
            Assert.Equal(0, model.OverdueCount);
            Assert.Equal(0, model.LateReturnCount);
            Assert.Equal(0, model.AvailableItemCount);
            Assert.Equal(0, model.AvailableCategoryCount);
            Assert.Empty(model.CurrentLoans);
            Assert.Empty(model.UpcomingPickups);
            Assert.Empty(model.RecentRequests);
        }

        [Fact]
        public async Task Overdue_loan_is_counted_and_listed_first()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 9, 30, 8, 0);
            var user = DashboardSeed.User(db, "BORROWER");
            var profile = DashboardSeed.Profile(db, user);
            var overdueItem = DashboardSeed.Item(db, "OVERDUE", DomainValues.EquipmentStatuses.Borrowed);
            var upcomingItem = DashboardSeed.Item(db, "UPCOMING", DomainValues.EquipmentStatuses.Borrowed);
            var overdue = DashboardSeed.Reservation(db, profile, overdueItem, now.AddDays(-2), now.AddHours(-3), DomainValues.ReservationStatuses.Approved);
            var upcoming = DashboardSeed.Reservation(db, profile, upcomingItem, now, now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, overdue, now.AddDays(-2));
            DashboardSeed.Release(db, upcoming, now);

            var model = await Service(db, now).GetBorrowerDashboardAsync(user.Id);

            Assert.Equal(2, model.ActiveLoanCount);
            Assert.Equal(1, model.OverdueCount);
            Assert.Equal("Overdue", model.CurrentLoans[0].DisplayStatus);
            Assert.Equal(1, model.CurrentLoans[0].DaysOverdue);
        }

        [Fact]
        public async Task Returned_loan_is_not_current()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 9, 30, 8, 0);
            var user = DashboardSeed.User(db, "BORROWER");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "RETURNED", DomainValues.EquipmentStatuses.Available);
            var reservation = DashboardSeed.Reservation(db, profile, item, now.AddDays(-2), now.AddDays(-1), DomainValues.ReservationStatuses.Approved);
            var release = DashboardSeed.Release(db, reservation, now.AddDays(-2));
            DashboardSeed.Return(db, release, now.AddHours(-12));

            var model = await Service(db, now).GetBorrowerDashboardAsync(user.Id);

            Assert.Equal(0, model.ActiveLoanCount);
            Assert.Empty(model.CurrentLoans);
        }

        [Fact]
        public async Task Approved_reservation_past_its_window_is_not_ready_for_pickup()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 9, 30, 8, 0);
            var user = DashboardSeed.User(db, "BORROWER");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "MISSED-PICKUP");
            DashboardSeed.Reservation(
                db,
                profile,
                item,
                now.AddDays(-2),
                now.AddDays(-1),
                DomainValues.ReservationStatuses.Approved);

            var model = await Service(db, now).GetBorrowerDashboardAsync(user.Id);

            Assert.Equal(0, model.ApprovedAwaitingPickupCount);
            Assert.Empty(model.UpcomingPickups);
            Assert.Equal("Missed pickup", model.RecentRequests.Single().DisplayStatus);
        }

        [Fact]
        public async Task Custodian_splits_loans_at_Manila_midnight()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 9, 30, 16, 0);
            var borrower = DashboardSeed.User(db, "BORROWER");
            var profile = DashboardSeed.Profile(db, borrower);
            var beforeMidnight = DashboardSeed.Item(db, "BEFORE-MIDNIGHT", DomainValues.EquipmentStatuses.Borrowed);
            var afterMidnight = DashboardSeed.Item(db, "AFTER-MIDNIGHT", DomainValues.EquipmentStatuses.Borrowed);
            var overdue = DashboardSeed.Reservation(db, profile, beforeMidnight, now.AddDays(-1), now.AddMinutes(-1), DomainValues.ReservationStatuses.Approved);
            var dueToday = DashboardSeed.Reservation(db, profile, afterMidnight, now.AddHours(-1), now.AddMinutes(1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, overdue, now.AddDays(-1));
            DashboardSeed.Release(db, dueToday, now.AddHours(-1));

            var model = await Service(db, now).GetCustodianDashboardAsync();

            Assert.Equal(1, model.OverdueCount);
            Assert.Single(model.Overdue);
            Assert.Equal(1, model.ReturnsDueTodayCount);
            Assert.Single(model.ReturnsDueToday);
            Assert.Equal(overdue.ReservationId, model.Desk[0].Row.ReservationId);
        }

        [Fact]
        public async Task Custodian_pickups_today_excludes_released_reservations()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 9, 30, 8, 0);
            var borrower = DashboardSeed.User(db, "BORROWER");
            var profile = DashboardSeed.Profile(db, borrower);
            var item = DashboardSeed.Item(db, "PICKUP");
            var pickup = DashboardSeed.Reservation(db, profile, item, now.AddHours(2), now.AddHours(4), DomainValues.ReservationStatuses.Approved);
            var service = Service(db, now);

            Assert.Equal(1, (await service.GetCustodianDashboardAsync()).PickupsTodayCount);
            DashboardSeed.Release(db, pickup, now);
            Assert.Equal(0, (await service.GetCustodianDashboardAsync()).PickupsTodayCount);
        }

        [Fact]
        public async Task Admin_attention_keeps_ineligible_alert_when_many_loans_are_overdue()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 9, 30, 8, 0);
            var ineligibleUser = DashboardSeed.User(db, "INELIGIBLE");
            DashboardSeed.Profile(db, ineligibleUser, eligible: false);

            for (var index = 0; index < 6; index++)
            {
                var user = DashboardSeed.User(db, $"OVERDUE-{index}");
                var profile = DashboardSeed.Profile(db, user);
                var item = DashboardSeed.Item(db, $"OVERDUE-ITEM-{index}", DomainValues.EquipmentStatuses.Borrowed);
                var reservation = DashboardSeed.Reservation(db, profile, item, now.AddDays(-3), now.AddHours(-index - 1), DomainValues.ReservationStatuses.Approved);
                DashboardSeed.Release(db, reservation, now.AddDays(-3));
            }

            var model = await Service(db, now).GetAdministratorDashboardAsync();

            Assert.Equal(6, model.OverdueLoanCount);
            Assert.Equal(5, model.NeedsAttention.Count);
            Assert.Contains(model.NeedsAttention, item => item.Title.Contains("eligibility review", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Admin_no_spare_alert_ignores_empty_categories()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            DashboardSeed.Category(db, "Empty Cameras");

            var model = await Service(db, Utc(2026, 9, 30, 8, 0))
                .GetAdministratorDashboardAsync();

            Assert.DoesNotContain(model.NeedsAttention, item => item.Title == "No spare items in Empty Cameras");
        }

        [Fact]
        public async Task Admin_empty_inventory_returns_zero_counts()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();

            var model = await Service(db, Utc(2026, 9, 30, 8, 0))
                .GetAdministratorDashboardAsync();

            Assert.Equal(0, model.TotalItems);
            Assert.Equal(0, model.ActiveCategories);
            Assert.Equal(0, model.ActiveLoanCount);
            Assert.Equal(0, model.OverdueLoanCount);
            Assert.Empty(model.InventoryByCategory);
        }

        private static DashboardService Service(
            SqliteDashboardDbContext db,
            DateTime nowUtc) => new(
                new UnitOfWork(db),
                NullLoggerFactory.Instance,
                new FixedTimeProvider(nowUtc));

        private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
            new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTime nowUtc)
            {
                _now = new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc));
            }

            public override DateTimeOffset GetUtcNow() => _now;
        }
    }
}
