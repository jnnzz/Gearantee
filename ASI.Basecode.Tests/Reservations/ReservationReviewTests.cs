using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Tests.Dashboard;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    /// <summary>
    /// WBS 39.00-42.00: the custodian review queue, approve, reject, and equipment release.
    /// Uses the existing reservation harness so the service runs against the real production
    /// model on SQLite with a fixed clock.
    /// </summary>
    public sealed class ReservationReviewTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 17, 0);

        private const string ReviewPermission = DomainValues.Permissions.ReservationReview;
        private const string ReleasePermission = DomainValues.Permissions.TransactionReleaseReturn;

        // ------------------------------------------------------------------ queue

        [Fact]
        public void Queue_lists_requests_with_borrower_details_and_per_row_actions()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var borrower = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, borrower);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var pending = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var approved = DashboardSeed.Reservation(db, profile, item, Now.AddDays(5), Now.AddDays(6), DomainValues.ReservationStatuses.Approved);
            Custodian(db, "CUS-1", ReviewPermission);

            var queue = ReservationsFor(db, Now).GetReviewQueue(null, null, 1);

            Assert.Equal(2, queue.TotalCount);
            Assert.Equal(1, queue.TotalPages);
            Assert.Equal(2, queue.Tabs.Single(tab => tab.Label == "All").Count);
            Assert.Equal(1, queue.Tabs.Single(tab => tab.Value == DomainValues.ReservationStatuses.Pending).Count);

            // Earliest window first, and the borrower travels with the row.
            Assert.Equal(pending.ReservationId, queue.Items[0].ReservationId);
            Assert.Equal("BRW-1 Test", queue.Items[0].BorrowerName);
            Assert.Equal("BRW-1", queue.Items[0].SchoolId);
            Assert.Equal("BSIT", queue.Items[0].Department);
            Assert.Equal("ITEM-1", queue.Items[0].ItemCode);

            // Only the pending request is decidable; the approved one can be released.
            Assert.True(queue.Items[0].CanApprove);
            Assert.True(queue.Items[0].CanReject);
            Assert.False(queue.Items[0].CanRelease);
            Assert.False(queue.Items[1].CanApprove);
            Assert.True(queue.Items[1].CanRelease);
            Assert.Equal(approved.ReservationId, queue.Items[1].ReservationId);
        }

        [Fact]
        public void Queue_expires_a_pending_request_whose_start_has_passed()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var stale = DashboardSeed.Reservation(db, profile, item, Now.AddHours(-2), Now.AddHours(4), DomainValues.ReservationStatuses.Pending);
            Custodian(db, "CUS-1", ReviewPermission);

            var queue = ReservationsFor(db, Now).GetReviewQueue(DomainValues.ReservationStatuses.Pending, null, 1);

            Assert.Empty(queue.Items);
            Assert.Equal(DomainValues.ReservationStatuses.Expired, Reload(db, stale.ReservationId).Status);
        }

        [Fact]
        public void Queue_filters_by_status_and_searches_items_and_borrowers()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-7"));
            var laptop = DashboardSeed.Item(db, "LAP-9");
            var camera = DashboardSeed.Item(db, "CAM-3");
            var first = DashboardSeed.Reservation(db, profile, laptop, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            DashboardSeed.Reservation(db, profile, camera, Now.AddDays(3), Now.AddDays(4), DomainValues.ReservationStatuses.Approved);
            Custodian(db, "CUS-1", ReviewPermission);
            var service = ReservationsFor(db, Now);

            var approvedOnly = service.GetReviewQueue(DomainValues.ReservationStatuses.Approved, null, 1);
            Assert.Single(approvedOnly.Items);

            var byItemCode = service.GetReviewQueue(null, "lap-9", 1);
            Assert.Equal(first.ReservationId, Assert.Single(byItemCode.Items).ReservationId);

            var byBorrowerCode = service.GetReviewQueue(null, "brw-7", 1);
            Assert.Equal(2, byBorrowerCode.TotalCount);

            Assert.Empty(service.GetReviewQueue(null, "nothing matches", 1).Items);
        }

        [Fact]
        public void Queue_pages_ten_requests_per_page()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            for (var index = 0; index < 21; index++)
            {
                DashboardSeed.Reservation(
                    db,
                    profile,
                    item,
                    Now.AddDays(1 + index),
                    Now.AddDays(2 + index),
                    DomainValues.ReservationStatuses.Pending);
            }
            Custodian(db, "CUS-1", ReviewPermission);
            var service = ReservationsFor(db, Now);

            var firstPage = service.GetReviewQueue(null, null, 1);
            Assert.Equal(21, firstPage.TotalCount);
            Assert.Equal(3, firstPage.TotalPages);
            Assert.Equal(10, firstPage.Items.Count);

            var secondPage = service.GetReviewQueue(null, null, 2);
            Assert.Equal(10, secondPage.Items.Count);
            Assert.Empty(firstPage.Items.Select(item => item.ReservationId).Intersect(secondPage.Items.Select(item => item.ReservationId)));
            Assert.Single(service.GetReviewQueue(null, null, 3).Items);

            // A page past the end clamps instead of returning nothing.
            Assert.Equal(3, service.GetReviewQueue(null, null, 99).Page);
        }

        // ---------------------------------------------------------------- approve

        [Fact]
        public void Approve_marks_the_request_approved_and_records_the_reviewer()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var borrower = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, borrower);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId);

            Assert.True(result.Succeeded, result.Message);

            var saved = Reload(db, reservation.ReservationId);
            Assert.Equal(DomainValues.ReservationStatuses.Approved, saved.Status);
            Assert.Equal(custodian.Id, saved.ReviewedByUserId);
            Assert.Equal(Now, saved.ReviewedAt);
            Assert.Equal(Now, saved.UpdatedAt);

            var audit = db.AdministrationAuditEvents.Single();
            Assert.Equal("ReservationApproved", audit.Action);
            Assert.Equal(custodian.Id, audit.ActorUserId);
            Assert.Equal(borrower.Id, audit.TargetUserId);
        }

        [Fact]
        public void Approve_refuses_when_the_item_is_already_booked_for_that_window()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var otherProfile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var target = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(3), DomainValues.ReservationStatuses.Pending);
            DashboardSeed.Reservation(db, otherProfile, item, Now.AddDays(2), Now.AddDays(4), DomainValues.ReservationStatuses.Approved);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, target.ReservationId);

            Assert.False(result.Succeeded);
            Assert.Contains("booked", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, target.ReservationId).Status);
        }

        [Theory]
        [InlineData(DomainValues.EquipmentStatuses.UnderMaintenance)]
        [InlineData(DomainValues.EquipmentStatuses.Unavailable)]
        public void Approve_refuses_when_item_status_becomes_unreservable_after_submission(string newStatus)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            item.ItemStatus = newStatus;
            db.SaveChanges();
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId);

            Assert.False(result.Succeeded);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, reservation.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
        }

        [Fact]
        public void Approve_refuses_when_item_is_archived_after_submission()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            item.IsArchived = true;
            db.SaveChanges();
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId);

            Assert.False(result.Succeeded);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, reservation.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
        }

        [Fact]
        public void Approve_refuses_when_category_becomes_inactive_after_submission()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var category = db.EquipmentCategories.Single(entry => entry.CategoryId == item.CategoryId);
            category.IsActive = false;
            db.SaveChanges();
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId);

            Assert.False(result.Succeeded);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, reservation.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
        }

        [Fact]
        public void Approve_expires_pending_request_if_its_start_passes_before_submit()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddMinutes(-1), Now.AddHours(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId);

            Assert.False(result.Succeeded);
            Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(DomainValues.ReservationStatuses.Expired, Reload(db, reservation.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
        }
        [Fact]
        public void Approve_refuses_a_request_that_is_no_longer_pending()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId);

            Assert.False(result.Succeeded);
            Assert.False(result.Forbidden);
            Assert.Equal(DomainValues.ReservationStatuses.Approved, Reload(db, reservation.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
        }

        [Fact]
        public void Approve_is_denied_when_the_permission_was_revoked_after_sign_in()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);
            Revoke(db, DomainValues.Roles.Custodian, ReviewPermission);

            var result = ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId);

            Assert.True(result.Forbidden);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, reservation.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
        }

        [Fact]
        public void Approve_is_denied_for_an_inactive_custodian()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);
            custodian.IsActive = false;
            db.SaveChanges();

            Assert.True(ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId).Forbidden);
        }

        [Fact]
        public void Approve_is_denied_for_a_non_custodian_who_holds_the_permission()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);
            var outsider = DashboardSeed.User(db, "BRW-9");
            GrantToOtherRole(db, outsider, DomainValues.Roles.Borrower, ReviewPermission);

            Assert.True(ReservationsFor(db, Now).ApproveReservation(outsider.Id, reservation.ReservationId).Forbidden);
            Assert.True(ReservationsFor(db, Now).ApproveReservation(null, reservation.ReservationId).Forbidden);
            Assert.True(ReservationsFor(db, Now).ApproveReservation(custodian.Id, reservation.ReservationId).Succeeded);
        }

        // ----------------------------------------------------------------- reject

        [Fact]
        public void Reject_requires_a_reason_and_records_it()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var borrower = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, borrower);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);
            var service = ReservationsFor(db, Now);

            var blank = service.RejectReservation(custodian.Id, reservation.ReservationId, "   ");
            Assert.False(blank.Succeeded);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, reservation.ReservationId).Status);

            var result = service.RejectReservation(custodian.Id, reservation.ReservationId, "  Item is reserved for a lab class.  ");
            Assert.True(result.Succeeded, result.Message);

            var saved = Reload(db, reservation.ReservationId);
            Assert.Equal(DomainValues.ReservationStatuses.Rejected, saved.Status);
            Assert.Equal("Item is reserved for a lab class.", saved.RejectionReason);
            Assert.Equal(custodian.Id, saved.ReviewedByUserId);
            Assert.Equal(Now, saved.ReviewedAt);

            var audit = db.AdministrationAuditEvents.Single();
            Assert.Equal("ReservationRejected", audit.Action);
            Assert.Contains("lab class", audit.DetailsJson);
        }

        [Fact]
        public void Reject_refuses_a_reason_longer_than_the_column()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).RejectReservation(custodian.Id, reservation.ReservationId, new string('x', 1001));

            Assert.False(result.Succeeded);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, reservation.ReservationId).Status);

            Assert.True(ReservationsFor(db, Now)
                .RejectReservation(custodian.Id, reservation.ReservationId, new string('x', 1000))
                .Succeeded);
        }

        [Fact]
        public void Reject_refuses_a_request_that_is_no_longer_pending()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Cancelled);
            var custodian = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).RejectReservation(custodian.Id, reservation.ReservationId, "Too late");

            Assert.False(result.Succeeded);
            Assert.Equal(DomainValues.ReservationStatuses.Cancelled, Reload(db, reservation.ReservationId).Status);
        }

        // ---------------------------------------------------------------- release

        [Fact]
        public void Release_records_the_handover_and_marks_the_item_borrowed()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var borrower = DashboardSeed.User(db, "BRW-1");
            var profile = DashboardSeed.Profile(db, borrower);
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var custodian = Custodian(db, "CUS-1", ReviewPermission, ReleasePermission);

            var result = ReservationsFor(db, Now).ReleaseEquipment(custodian.Id, reservation.ReservationId, "  Charger and sleeve included.  ");

            Assert.True(result.Succeeded, result.Message);

            var release = db.ReleaseRecords.Single();
            Assert.Equal(reservation.ReservationId, release.ReservationId);
            Assert.Equal(custodian.Id, release.ReleasedByUserId);
            Assert.Equal(Now, release.ActualReleaseAt);
            Assert.Equal("Charger and sleeve included.", release.Notes);

            Assert.Equal(DomainValues.EquipmentStatuses.Borrowed, ReloadItem(db, item.EquipmentId).ItemStatus);
            Assert.Equal(DomainValues.ReservationStatuses.Approved, Reload(db, reservation.ReservationId).Status);

            var audit = db.AdministrationAuditEvents.Single();
            Assert.Equal("EquipmentReleased", audit.Action);
            Assert.Equal(custodian.Id, audit.ActorUserId);
            Assert.Equal(borrower.Id, audit.TargetUserId);
        }

        [Fact]
        public void Release_twice_is_refused_and_only_one_record_exists()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var custodian = Custodian(db, "CUS-1", ReviewPermission, ReleasePermission);
            var service = ReservationsFor(db, Now);

            Assert.True(service.ReleaseEquipment(custodian.Id, reservation.ReservationId, null).Succeeded);
            var second = service.ReleaseEquipment(custodian.Id, reservation.ReservationId, null);

            Assert.False(second.Succeeded);
            Assert.Single(db.ReleaseRecords);
            Assert.Single(db.AdministrationAuditEvents);
        }

        [Fact]
        public void Release_refuses_a_request_that_has_not_been_approved()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var custodian = Custodian(db, "CUS-1", ReviewPermission, ReleasePermission);

            var result = ReservationsFor(db, Now).ReleaseEquipment(custodian.Id, reservation.ReservationId, null);

            Assert.False(result.Succeeded);
            Assert.Contains("Approve", result.Message);
            Assert.Empty(db.ReleaseRecords);
            Assert.Equal(DomainValues.EquipmentStatuses.Available, ReloadItem(db, item.EquipmentId).ItemStatus);
        }

        [Fact]
        public void Release_refuses_while_the_item_is_still_out_to_someone_else()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var firstProfile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var secondProfile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-2"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var outstanding = DashboardSeed.Reservation(db, firstProfile, item, Now.AddDays(-2), Now.AddDays(3), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, outstanding, Now.AddDays(-2));
            var target = DashboardSeed.Reservation(db, secondProfile, item, Now.AddDays(4), Now.AddDays(5), DomainValues.ReservationStatuses.Approved);
            var custodian = Custodian(db, "CUS-1", ReviewPermission, ReleasePermission);

            var result = ReservationsFor(db, Now).ReleaseEquipment(custodian.Id, target.ReservationId, null);

            Assert.False(result.Succeeded);
            Assert.Contains("on loan", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(db.ReleaseRecords);
        }

        [Fact]
        public void Release_refuses_an_item_that_is_under_maintenance()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            item.ItemStatus = DomainValues.EquipmentStatuses.UnderMaintenance;
            db.SaveChanges();
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var custodian = Custodian(db, "CUS-1", ReviewPermission, ReleasePermission);

            var result = ReservationsFor(db, Now).ReleaseEquipment(custodian.Id, reservation.ReservationId, null);

            Assert.False(result.Succeeded);
            Assert.Contains("UnderMaintenance", result.Message.Replace(" ", string.Empty));
            Assert.Equal(DomainValues.EquipmentStatuses.UnderMaintenance, ReloadItem(db, item.EquipmentId).ItemStatus);
        }

        /// <summary>
        /// Permissions are granted per role, so a custodian role that may review but not release
        /// must not be able to record a handover.
        /// </summary>
        [Fact]
        public void Release_is_denied_for_a_custodian_who_may_only_review()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var reviewerOnly = Custodian(db, "CUS-1", ReviewPermission);

            var result = ReservationsFor(db, Now).ReleaseEquipment(reviewerOnly.Id, reservation.ReservationId, null);

            Assert.True(result.Forbidden);
            Assert.Empty(db.ReleaseRecords);
            Assert.Equal(DomainValues.EquipmentStatuses.Available, ReloadItem(db, item.EquipmentId).ItemStatus);
        }

        /// <summary>
        /// The reverse also holds: a custodian role that may release but not review cannot approve.
        /// </summary>
        [Fact]
        public void Approve_is_denied_for_a_custodian_who_may_only_release()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var releaserOnly = Custodian(db, "CUS-1", ReleasePermission);

            var result = ReservationsFor(db, Now).ApproveReservation(releaserOnly.Id, reservation.ReservationId);

            Assert.True(result.Forbidden);
            Assert.Equal(DomainValues.ReservationStatuses.Pending, Reload(db, reservation.ReservationId).Status);
            Assert.Empty(db.AdministrationAuditEvents);
        }

        // ---------------------------------------------------------------- helpers

        private static ApplicationUser Custodian(
            SqliteDashboardDbContext db,
            string code,
            params string[] permissionNames)
        {
            var actor = DashboardSeed.User(db, code);
            var role = db.Roles.FirstOrDefault(item => item.Name == DomainValues.Roles.Custodian);
            if (role == null)
            {
                role = new IdentityRole(DomainValues.Roles.Custodian);
                db.Roles.Add(role);
                db.SaveChanges();
            }

            if (!db.UserRoles.Any(link => link.UserId == actor.Id && link.RoleId == role.Id))
            {
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = actor.Id, RoleId = role.Id });
            }

            foreach (var name in permissionNames)
            {
                var permission = EnsurePermission(db, name);
                if (!db.RolePermissions.Any(link => link.RoleId == role.Id && link.PermissionId == permission.PermissionId))
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.PermissionId
                    });
                }
            }

            db.SaveChanges();
            return actor;
        }

        private static Permission EnsurePermission(SqliteDashboardDbContext db, string name)
        {
            var permission = db.Permissions.FirstOrDefault(item => item.PermissionName == name);
            if (permission != null)
            {
                return permission;
            }

            permission = new Permission { PermissionName = name, Description = name };
            db.Permissions.Add(permission);
            db.SaveChanges();
            return permission;
        }

        private static void Revoke(SqliteDashboardDbContext db, string roleName, string permissionName)
        {
            var role = db.Roles.Single(item => item.Name == roleName);
            var permission = db.Permissions.Single(item => item.PermissionName == permissionName);
            var link = db.RolePermissions.Single(item =>
                item.RoleId == role.Id && item.PermissionId == permission.PermissionId);

            db.RolePermissions.Remove(link);
            db.SaveChanges();
        }

        private static void GrantToOtherRole(
            SqliteDashboardDbContext db,
            ApplicationUser user,
            string roleName,
            string permissionName)
        {
            var role = db.Roles.FirstOrDefault(item => item.Name == roleName);
            if (role == null)
            {
                role = new IdentityRole(roleName);
                db.Roles.Add(role);
                db.SaveChanges();
            }

            if (!db.UserRoles.Any(link => link.UserId == user.Id && link.RoleId == role.Id))
            {
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
            }

            var permission = EnsurePermission(db, permissionName);
            if (!db.RolePermissions.Any(link => link.RoleId == role.Id && link.PermissionId == permission.PermissionId))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.PermissionId
                });
            }

            db.SaveChanges();
        }

        private static Reservation Reload(SqliteDashboardDbContext db, long reservationId)
        {
            db.ChangeTracker.Clear();
            return db.Reservations.Single(item => item.ReservationId == reservationId);
        }

        private static EquipmentItem ReloadItem(SqliteDashboardDbContext db, long equipmentId)
        {
            db.ChangeTracker.Clear();
            return db.EquipmentItems.Single(item => item.EquipmentId == equipmentId);
        }
    }
}
