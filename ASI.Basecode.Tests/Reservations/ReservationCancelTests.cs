using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    public sealed class ReservationCancelTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 7, 0);

        private static Reservation Reload(SqliteDashboardDbContext db, long reservationId)
        {
            db.ChangeTracker.Clear();
            return db.Reservations.Single(reservation => reservation.ReservationId == reservationId);
        }

        [Theory]
        [InlineData(DomainValues.ReservationStatuses.Pending)]
        [InlineData(DomainValues.ReservationStatuses.Approved)]
        public void Owner_can_cancel_pending_or_unreleased_approved(string status)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), status);

            var result = ReservationsFor(db, Now).CancelReservation(reservation.ReservationId, user.Id);

            Assert.True(result.Succeeded, result.Message);
            var saved = Reload(db, reservation.ReservationId);
            Assert.Equal(DomainValues.ReservationStatuses.Cancelled, saved.Status);
            Assert.Equal(Now, saved.CancelledAt);
            Assert.Equal(Now, saved.UpdatedAt);
        }

        [Fact]
        public void Cancelled_approved_reservation_no_longer_blocks_the_item()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var other = DashboardSeed.User(db, "BRW-2");
            DashboardSeed.Profile(db, other);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var service = ReservationsFor(db, Now);

            service.CancelReservation(reservation.ReservationId, user.Id);
            db.ChangeTracker.Clear();
            var result = service.SubmitReservation(new ReservationCreateViewModel
            {
                EquipmentId = item.EquipmentId,
                ReservationStart = ManilaClock.ToLocal(Now.AddDays(1)),
                ReservationEnd = ManilaClock.ToLocal(Now.AddDays(2)),
                Purpose = "Lab class"
            }, other.Id);

            Assert.True(result.Succeeded, result.Message);
        }

        [Fact]
        public void Someone_elses_reservation_is_not_found_and_unchanged()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            DashboardSeed.Profile(db, user);
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var theirs = DashboardSeed.Reservation(db, other, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var service = ReservationsFor(db, Now);

            var result = service.CancelReservation(theirs.ReservationId, user.Id);

            Assert.False(result.Succeeded);
            Assert.True(result.NotFound);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, theirs.ReservationId).Status);
            Assert.True(service.CancelReservation(999, user.Id).NotFound);
            Assert.True(service.CancelReservation(theirs.ReservationId, "no-such-user").NotFound);
        }

        [Theory]
        [InlineData(DomainValues.ReservationStatuses.Rejected, "rejected")]
        [InlineData(DomainValues.ReservationStatuses.Cancelled, "already cancelled")]
        [InlineData(DomainValues.ReservationStatuses.Expired, "expired")]
        public void Closed_reservations_are_refused(string status, string expected)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), status);

            var result = ReservationsFor(db, Now).CancelReservation(reservation.ReservationId, user.Id);

            Assert.False(result.Succeeded);
            Assert.False(result.NotFound);
            Assert.Contains(expected, result.Message);
            Assert.Equal(status, Reload(db, reservation.ReservationId).Status);
        }

        [Fact]
        public void Released_and_returned_reservations_are_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var onLoan = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, onLoan, Now.AddDays(-1));
            var returned = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-5), Now.AddDays(-4), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Return(db, DashboardSeed.Release(db, returned, Now.AddDays(-5)), Now.AddDays(-4));
            var service = ReservationsFor(db, Now);

            var onLoanResult = service.CancelReservation(onLoan.ReservationId, user.Id);
            var returnedResult = service.CancelReservation(returned.ReservationId, user.Id);

            Assert.False(onLoanResult.Succeeded);
            Assert.Contains("already picked up", onLoanResult.Message);
            Assert.False(returnedResult.Succeeded);
            Assert.Contains("already been returned", returnedResult.Message);
            Assert.Equal(DomainValues.ReservationStatuses.Approved, Reload(db, onLoan.ReservationId).Status);
            Assert.Equal(DomainValues.ReservationStatuses.Approved, Reload(db, returned.ReservationId).Status);
        }

        [Fact]
        public void Stale_pending_is_expired_instead_of_cancelled()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var stale = DashboardSeed.Reservation(db, profile, item, Now.AddHours(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Pending);

            var result = ReservationsFor(db, Now).CancelReservation(stale.ReservationId, user.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("expired", result.Message);
            var saved = Reload(db, stale.ReservationId);
            Assert.Equal(DomainValues.ReservationStatuses.Expired, saved.Status);
            Assert.Null(saved.CancelledAt);
        }

        [Fact]
        public void Second_cancel_is_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var service = ReservationsFor(db, Now);

            var first = service.CancelReservation(reservation.ReservationId, user.Id);
            db.ChangeTracker.Clear();
            var second = service.CancelReservation(reservation.ReservationId, user.Id);

            Assert.True(first.Succeeded, first.Message);
            Assert.False(second.Succeeded);
            Assert.Contains("already cancelled", second.Message);
        }

        [Fact]
        public void Listing_cancel_flag_matches_the_cancel_rule()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var statuses = new[]
            {
                DomainValues.ReservationStatuses.Pending,
                DomainValues.ReservationStatuses.Approved,
                DomainValues.ReservationStatuses.Rejected,
                DomainValues.ReservationStatuses.Cancelled,
                DomainValues.ReservationStatuses.Expired
            };
            for (var i = 0; i < statuses.Length; i++)
            {
                DashboardSeed.Reservation(db, profile, item, Now.AddDays(i + 1), Now.AddDays(i + 2), statuses[i]);
            }
            var released = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, released, Now.AddDays(-1));
            var service = ReservationsFor(db, Now);

            var rows = service.GetMyReservations(user.Id, null, 1).Items;
            foreach (var row in rows)
            {
                db.ChangeTracker.Clear();
                var result = service.CancelReservation(row.ReservationId, user.Id);
                Assert.Equal(row.CanCancel, result.Succeeded);
            }
            Assert.Equal(2, rows.Count(row => row.CanCancel));
        }

        [Fact]
        public void Cancel_is_a_post_with_antiforgery()
        {
            var cancel = typeof(ReservationController).GetMethod(nameof(ReservationController.Cancel));

            Assert.NotEmpty(cancel.GetCustomAttributes(typeof(HttpPostAttribute), true));
            Assert.NotEmpty(cancel.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
        }
    }
}
