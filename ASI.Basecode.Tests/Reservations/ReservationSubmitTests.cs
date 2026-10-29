using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using System;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    public sealed class ReservationSubmitTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 7, 0);

        private static ReservationCreateViewModel Request(EquipmentItem item, DateTime startUtc, DateTime endUtc, string purpose = "Thesis defense") =>
            new ReservationCreateViewModel
            {
                EquipmentId = item.EquipmentId,
                ReservationStart = ManilaClock.ToLocal(startUtc),
                ReservationEnd = ManilaClock.ToLocal(endUtc),
                Purpose = purpose
            };

        [Fact]
        public void Valid_request_is_saved_as_pending_with_utc_times()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");

            var result = ReservationsFor(db, Now).SubmitReservation(
                Request(item, Now.AddDays(1), Now.AddDays(1).AddHours(3), "  Thesis defense  "), user.Id);

            Assert.True(result.Succeeded, result.Message);
            var saved = db.Reservations.Single();
            Assert.Equal(result.ReservationId, saved.ReservationId);
            Assert.Equal(profile.BorrowerProfileId, saved.BorrowerProfileId);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, saved.Status);
            Assert.Equal(Now.AddDays(1), saved.ReservationStart);
            Assert.Equal(Now.AddDays(1).AddHours(3), saved.ReservationEnd);
            Assert.Equal("Thesis defense", saved.Purpose);
            Assert.Equal(Now, saved.RequestedAt);
            Assert.Equal(Now, saved.CreatedAt);
            Assert.Equal(Now, saved.UpdatedAt);
            Assert.Null(saved.ReviewedByUserId);
        }

        [Fact]
        public void Borrowed_item_can_be_reserved_after_its_loan()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1", DomainValues.EquipmentStatuses.Borrowed);
            var loan = DashboardSeed.Reservation(db, other, item, Now.AddDays(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, loan, Now.AddDays(-1));

            var result = ReservationsFor(db, Now).SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2)), user.Id);

            Assert.True(result.Succeeded, result.Message);
        }

        [Theory]
        [InlineData(false, true, "deactivated")]
        [InlineData(true, false, "not currently eligible")]
        public void Inactive_or_ineligible_borrower_is_refused(bool active, bool eligible, string expected)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1", active);
            DashboardSeed.Profile(db, user, eligible);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var service = ReservationsFor(db, Now);

            var result = service.SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2)), user.Id);

            Assert.False(result.Succeeded);
            Assert.Contains(expected, result.Message);
            Assert.NotNull(service.CheckBorrowerEligibility(user.Id));
            Assert.Empty(db.Reservations);
        }

        [Fact]
        public void User_without_borrower_profile_is_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var item = DashboardSeed.Item(db, "ITEM-1");

            var result = ReservationsFor(db, Now).SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2)), user.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("no borrower profile", result.Message);
            Assert.Empty(db.Reservations);
        }

        [Theory]
        [InlineData(DomainValues.EquipmentStatuses.UnderMaintenance, false, false)]
        [InlineData(DomainValues.EquipmentStatuses.Unavailable, false, false)]
        [InlineData(DomainValues.EquipmentStatuses.Available, true, false)]
        [InlineData(DomainValues.EquipmentStatuses.Available, false, true)]
        public void Unreservable_item_is_refused(string status, bool archived, bool categoryInactive)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1", status);
            item.IsArchived = archived;
            DashboardSeed.Category(db).IsActive = !categoryInactive;
            db.SaveChanges();

            var result = ReservationsFor(db, Now).SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2)), user.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("can't be reserved", result.Message);
            Assert.Empty(db.Reservations);
        }

        [Fact]
        public void Missing_item_is_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);

            var result = ReservationsFor(db, Now).SubmitReservation(new ReservationCreateViewModel
            {
                EquipmentId = 999,
                ReservationStart = ManilaClock.ToLocal(Now.AddDays(1)),
                ReservationEnd = ManilaClock.ToLocal(Now.AddDays(2)),
                Purpose = "Thesis defense"
            }, user.Id);

            Assert.False(result.Succeeded);
        }

        [Theory]
        [InlineData(24, 24, "after the start")]   // start == end
        [InlineData(26, 24, "after the start")]   // end before start
        [InlineData(-1, 2, "in the future")]      // start in the past
        [InlineData(0, 2, "in the future")]       // start exactly now
        public void Invalid_window_is_refused(int startOffsetHours, int endOffsetHours, string expected)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");

            var result = ReservationsFor(db, Now).SubmitReservation(
                Request(item, Now.AddHours(startOffsetHours), Now.AddHours(endOffsetHours)), user.Id);

            Assert.False(result.Succeeded);
            Assert.Contains(expected, result.Message);
            Assert.Empty(db.Reservations);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Blank_purpose_is_refused(string purpose)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");

            var result = ReservationsFor(db, Now).SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2), purpose), user.Id);

            Assert.False(result.Succeeded);
            Assert.Empty(db.Reservations);
        }

        [Fact]
        public void Overlap_with_approved_reservation_is_refused_but_touching_is_allowed()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            DashboardSeed.Reservation(db, other, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var service = ReservationsFor(db, Now);

            var overlapping = service.SubmitReservation(Request(item, Now.AddDays(1).AddHours(12), Now.AddDays(3)), user.Id);
            var touching = service.SubmitReservation(Request(item, Now.AddDays(2), Now.AddDays(3)), user.Id);

            Assert.False(overlapping.Succeeded);
            Assert.Contains("already booked", overlapping.Message);
            Assert.True(touching.Succeeded, touching.Message);
        }

        [Fact]
        public void Overdue_loan_blocks_new_requests()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1", DomainValues.EquipmentStatuses.Borrowed);
            var loan = DashboardSeed.Reservation(db, other, item, Now.AddDays(-3), Now.AddDays(-1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, loan, Now.AddDays(-3));

            var result = ReservationsFor(db, Now).SubmitReservation(Request(item, Now.AddDays(5), Now.AddDays(6)), user.Id);

            Assert.False(result.Succeeded);
        }

        [Fact]
        public void Pending_requests_from_other_borrowers_do_not_block()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            DashboardSeed.Reservation(db, other, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);

            var result = ReservationsFor(db, Now).SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2)), user.Id);

            Assert.True(result.Succeeded, result.Message);
        }

        [Fact]
        public void Duplicate_own_pending_request_is_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var otherItem = DashboardSeed.Item(db, "ITEM-2");
            var service = ReservationsFor(db, Now);

            var first = service.SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2)), user.Id);
            var duplicate = service.SubmitReservation(Request(item, Now.AddDays(1).AddHours(6), Now.AddDays(3)), user.Id);
            var differentItem = service.SubmitReservation(Request(otherItem, Now.AddDays(1), Now.AddDays(2)), user.Id);
            var laterWindow = service.SubmitReservation(Request(item, Now.AddDays(2), Now.AddDays(3)), user.Id);

            Assert.True(first.Succeeded, first.Message);
            Assert.False(duplicate.Succeeded);
            Assert.Contains("already have a pending request", duplicate.Message);
            Assert.True(differentItem.Succeeded, differentItem.Message);
            Assert.True(laterWindow.Succeeded, laterWindow.Message);
        }

        [Theory]
        [InlineData(DomainValues.ReservationStatuses.Cancelled)]
        [InlineData(DomainValues.ReservationStatuses.Rejected)]
        [InlineData(DomainValues.ReservationStatuses.Expired)]
        public void Closed_own_requests_do_not_count_as_duplicates(string status)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), status);

            var result = ReservationsFor(db, Now).SubmitReservation(Request(item, Now.AddDays(1), Now.AddDays(2)), user.Id);

            Assert.True(result.Succeeded, result.Message);
        }
    }
}
