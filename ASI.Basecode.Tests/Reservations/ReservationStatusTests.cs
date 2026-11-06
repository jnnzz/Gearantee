using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using System;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    public sealed class ReservationStatusTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 7, 0);

        [Fact]
        public void List_shows_only_my_reservations_newest_first()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var mine = DashboardSeed.Profile(db, user);
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var older = DashboardSeed.Reservation(db, mine, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending, Now.AddDays(-2));
            var newer = DashboardSeed.Reservation(db, mine, item, Now.AddDays(3), Now.AddDays(4), DomainValues.ReservationStatuses.Pending, Now.AddDays(-1));
            DashboardSeed.Reservation(db, other, item, Now.AddDays(5), Now.AddDays(6), DomainValues.ReservationStatuses.Pending);

            var model = ReservationsFor(db, Now).GetMyReservations(user.Id, null, 1);

            Assert.Equal(new[] { newer.ReservationId, older.ReservationId }, model.Items.Select(row => row.ReservationId));
            Assert.Equal(2, model.TotalCount);
            Assert.Equal(2, model.Tabs.Single(tab => tab.Label == "All").Count);
            Assert.Equal(ManilaClock.ToLocal(Now.AddDays(3)), model.Items[0].StartLocal);
            Assert.Equal("ITEM-1", model.Items[0].ItemCode);
        }

        [Theory]
        [InlineData("Approved", 1)]
        [InlineData("approved", 1)]
        [InlineData("Rejected", 1)]
        [InlineData("Expired", 0)]
        [InlineData("nonsense", 3)]
        [InlineData("", 3)]
        public void Status_filter_matches_stored_status(string status, int expected)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(3), Now.AddDays(4), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(5), Now.AddDays(6), DomainValues.ReservationStatuses.Rejected);

            var model = ReservationsFor(db, Now).GetMyReservations(user.Id, status, 1);

            Assert.Equal(expected, model.Items.Count);
            Assert.Equal(1, model.Tabs.Single(tab => tab.Value == DomainValues.ReservationStatuses.Approved).Count);
            Assert.Equal(6, model.Tabs.Count);
        }

        [Fact]
        public void Someone_elses_reservation_is_not_found()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var theirs = DashboardSeed.Reservation(db, other, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var service = ReservationsFor(db, Now);

            Assert.Null(service.RetrieveMyReservation(theirs.ReservationId, user.Id));
            Assert.Null(service.RetrieveMyReservation(999, user.Id));
            Assert.NotNull(service.RetrieveMyReservation(theirs.ReservationId, "brw-2"));
        }

        [Fact]
        public void User_without_profile_sees_an_empty_list()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");

            var model = ReservationsFor(db, Now).GetMyReservations(user.Id, null, 1);

            Assert.Empty(model.Items);
            Assert.All(model.Tabs, tab => Assert.Equal(0, tab.Count));
        }

        [Fact]
        public void Stale_pending_request_is_saved_as_expired_when_viewed()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var stale = DashboardSeed.Reservation(db, profile, item, Now.AddHours(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Pending);
            var future = DashboardSeed.Reservation(db, profile, item, Now.AddDays(2), Now.AddDays(3), DomainValues.ReservationStatuses.Pending);

            var model = ReservationsFor(db, Now).GetMyReservations(user.Id, "Expired", 1);

            var row = Assert.Single(model.Items);
            Assert.Equal(stale.ReservationId, row.ReservationId);
            Assert.Equal("Expired", row.DisplayStatus);
            Assert.False(row.CanCancel);

            db.ChangeTracker.Clear();
            var saved = db.Reservations.Single(r => r.ReservationId == stale.ReservationId);
            Assert.Equal(DomainValues.ReservationStatuses.Expired, saved.Status);
            Assert.Equal(Now, saved.UpdatedAt);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, db.Reservations.Single(r => r.ReservationId == future.ReservationId).Status);
        }

        [Fact]
        public void Details_also_expires_a_stale_pending_request()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var stale = DashboardSeed.Reservation(db, profile, item, Now.AddHours(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Pending);

            var details = ReservationsFor(db, Now).RetrieveMyReservation(stale.ReservationId, user.Id);

            Assert.Equal(DomainValues.ReservationStatuses.Expired, details.Status);
        }

        [Fact]
        public void Display_status_and_cancel_flag_follow_the_loan_state()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var pending = DashboardSeed.Reservation(db, profile, item, Now.AddDays(10), Now.AddDays(11), DomainValues.ReservationStatuses.Pending);
            var awaiting = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var missed = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-3), Now.AddDays(-2), DomainValues.ReservationStatuses.Approved);
            var borrowed = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-1), Now.AddDays(3), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, borrowed, Now.AddDays(-1));
            var overdue = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-5), Now.AddDays(-4), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, overdue, Now.AddDays(-5));
            var returned = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-8), Now.AddDays(-7), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Return(db, DashboardSeed.Release(db, returned, Now.AddDays(-8)), Now.AddDays(-7));
            var rejected = DashboardSeed.Reservation(db, profile, item, Now.AddDays(4), Now.AddDays(5), DomainValues.ReservationStatuses.Rejected);
            var cancelled = DashboardSeed.Reservation(db, profile, item, Now.AddDays(6), Now.AddDays(7), DomainValues.ReservationStatuses.Cancelled);

            var rows = ReservationsFor(db, Now).GetMyReservations(user.Id, null, 1).Items
                .ToDictionary(row => row.ReservationId);

            Assert.Equal(("Pending", true), (rows[pending.ReservationId].DisplayStatus, rows[pending.ReservationId].CanCancel));
            Assert.Equal(("Awaiting Release", true), (rows[awaiting.ReservationId].DisplayStatus, rows[awaiting.ReservationId].CanCancel));
            Assert.Equal(("Missed pickup", true), (rows[missed.ReservationId].DisplayStatus, rows[missed.ReservationId].CanCancel));
            Assert.Equal(("Borrowed", false), (rows[borrowed.ReservationId].DisplayStatus, rows[borrowed.ReservationId].CanCancel));
            Assert.Equal(("Overdue", false), (rows[overdue.ReservationId].DisplayStatus, rows[overdue.ReservationId].CanCancel));
            Assert.Equal(("Returned", false), (rows[returned.ReservationId].DisplayStatus, rows[returned.ReservationId].CanCancel));
            Assert.Equal(("Rejected", false), (rows[rejected.ReservationId].DisplayStatus, rows[rejected.ReservationId].CanCancel));
            Assert.Equal(("Cancelled", false), (rows[cancelled.ReservationId].DisplayStatus, rows[cancelled.ReservationId].CanCancel));
        }

        [Fact]
        public void Details_show_rejection_reason_reviewer_and_loan_times()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var custodian = DashboardSeed.User(db, "CUS-1");
            var item = DashboardSeed.Item(db, "ITEM-1");
            var rejected = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Rejected);
            rejected.RejectionReason = "Item is reserved for an exam.";
            rejected.ReviewedByUserId = custodian.Id;
            rejected.ReviewedAt = Now.AddHours(-2);
            var returned = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-3), Now.AddDays(-2), DomainValues.ReservationStatuses.Approved);
            db.SaveChanges();
            DashboardSeed.Return(db, DashboardSeed.Release(db, returned, Now.AddDays(-3)), Now.AddDays(-2), DomainValues.ReturnConditions.Good);
            var service = ReservationsFor(db, Now);

            var rejectedDetails = service.RetrieveMyReservation(rejected.ReservationId, user.Id);
            var returnedDetails = service.RetrieveMyReservation(returned.ReservationId, user.Id);

            Assert.Equal("Item is reserved for an exam.", rejectedDetails.RejectionReason);
            Assert.Equal("CUS-1 Test", rejectedDetails.ReviewerName);
            Assert.Equal(ManilaClock.ToLocal(Now.AddHours(-2)), rejectedDetails.ReviewedAtLocal);
            Assert.Equal("Dashboard test", rejectedDetails.Purpose);
            Assert.Equal($"RSV-{rejected.ReservationId}", rejectedDetails.ReservationCode);
            Assert.Equal(ManilaClock.ToLocal(Now.AddDays(-3)), returnedDetails.ReleasedAtLocal);
            Assert.Equal(ManilaClock.ToLocal(Now.AddDays(-2)), returnedDetails.ReturnedAtLocal);
            Assert.Equal(DomainValues.ReturnConditions.Good, returnedDetails.ReturnedCondition);
            Assert.Equal("Returned", returnedDetails.DisplayStatus);
        }

        [Fact]
        public void Paging_clamps_to_the_last_page()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            for (var i = 0; i < 11; i++)
            {
                DashboardSeed.Reservation(db, profile, item, Now.AddDays(i + 1), Now.AddDays(i + 2), DomainValues.ReservationStatuses.Cancelled);
            }

            var model = ReservationsFor(db, Now).GetMyReservations(user.Id, null, 99);

            Assert.Equal(10, ReservationsFor(db, Now).GetMyReservations(user.Id, null, 1).Items.Count);
            Assert.Equal(2, model.TotalPages);
            Assert.Equal(2, model.Page);
            Assert.Single(model.Items);
        }
    }
}
