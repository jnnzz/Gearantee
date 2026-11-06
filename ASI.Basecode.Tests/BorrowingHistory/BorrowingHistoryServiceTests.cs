using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.BorrowingHistory
{
    public sealed class BorrowingHistoryServiceTests
    {
        [Fact]
        public async Task History_includes_lifecycle_dates_and_filters_recorded_late_returns()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 10, 14, 4);
            var user = DashboardSeed.User(db, "STUDENT-A");
            var profile = DashboardSeed.Profile(db, user);
            var category = DashboardSeed.Category(db, "Cameras");
            var item = DashboardSeed.Item(
                db,
                "CAM-1",
                DomainValues.EquipmentStatuses.Available,
                "Cameras");

            var start = ManilaClock.ToUtc(new DateTime(2026, 10, 12, 10, 0, 0));
            var due = start.AddHours(2);
            var lateReservation = DashboardSeed.Reservation(
                db,
                profile,
                item,
                start,
                due,
                DomainValues.ReservationStatuses.Approved);
            var lateRelease = DashboardSeed.Release(db, lateReservation, start);
            DashboardSeed.Return(db, lateRelease, due.AddHours(2));
            var returnedRecord = db.ReturnRecords.Single(record =>
                record.ReleaseRecordId == lateRelease.ReleaseRecordId);
            db.LateReturns.Add(new LateReturn
            {
                ReturnRecordId = returnedRecord.ReturnRecordId,
                DueAt = due,
                ReturnedAt = due.AddHours(2),
                DaysLate = 1,
                PenaltyStatus = "Pending"
            });

            var overdueStart = ManilaClock.ToUtc(new DateTime(2026, 10, 13, 8, 0, 0));
            var overdue = DashboardSeed.Reservation(
                db,
                profile,
                item,
                overdueStart,
                overdueStart.AddHours(1),
                DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, overdue, overdueStart);
            db.SaveChanges();

            var cancelledStart = ManilaClock.ToUtc(new DateTime(2026, 10, 11, 8, 0, 0));
            var cancelled = DashboardSeed.Reservation(
                db,
                profile,
                item,
                cancelledStart,
                cancelledStart.AddHours(1),
                DomainValues.ReservationStatuses.Cancelled);

            var service = Service(db, now);
            var all = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel(),
                user.Id,
                onlyOwnHistory: false);

            Assert.Equal(3, all.TotalCount);
            Assert.Contains(all.Items, row => row.ReservationId == lateReservation.ReservationId &&
                row.Status == "Returned" && row.HasLateReturn && row.DaysLate == 1);
            Assert.Contains(all.Items, row => row.ReservationId == overdue.ReservationId &&
                row.Status == "Overdue" && !row.HasLateReturn);
            Assert.Contains(all.Items, row => row.ReservationId == cancelled.ReservationId &&
                row.Status == DomainValues.ReservationStatuses.Cancelled);

            var lateOnly = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel { LateReturn = "yes" },
                user.Id,
                onlyOwnHistory: false);
            Assert.Single(lateOnly.Items);
            Assert.Equal(lateReservation.ReservationId, lateOnly.Items[0].ReservationId);

            var borrowerFiltered = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel { Borrower = "STUDENT-A" },
                user.Id,
                onlyOwnHistory: false);
            Assert.Equal(3, borrowerFiltered.TotalCount);

            var itemFiltered = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel { Item = "CAM-1" },
                user.Id,
                onlyOwnHistory: false);
            Assert.Equal(3, itemFiltered.TotalCount);

            var overdueOnly = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel { Status = "overdue" },
                user.Id,
                onlyOwnHistory: false);
            Assert.Single(overdueOnly.Items);
            Assert.Equal(overdue.ReservationId, overdueOnly.Items[0].ReservationId);

            var dateAndCategory = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel
                {
                    CategoryId = category.CategoryId,
                    FromDate = new DateTime(2026, 10, 12),
                    ToDate = new DateTime(2026, 10, 12)
                },
                user.Id,
                onlyOwnHistory: false);
            Assert.Single(dateAndCategory.Items);
            Assert.Equal(lateReservation.ReservationId, dateAndCategory.Items[0].ReservationId);
        }

        [Fact]
        public async Task Borrower_scope_and_search_never_return_another_borrowers_records()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 10, 14, 4);
            var userA = DashboardSeed.User(db, "STUDENT-A");
            var userB = DashboardSeed.User(db, "STUDENT-B");
            var profileA = DashboardSeed.Profile(db, userA);
            var profileB = DashboardSeed.Profile(db, userB);
            var item = DashboardSeed.Item(db, "CAM-1");
            var start = now.AddDays(-1);
            DashboardSeed.Reservation(
                db,
                profileA,
                item,
                start,
                start.AddHours(1),
                DomainValues.ReservationStatuses.Cancelled);
            DashboardSeed.Reservation(
                db,
                profileB,
                item,
                start.AddDays(1),
                start.AddDays(1).AddHours(1),
                DomainValues.ReservationStatuses.Cancelled);

            var service = Service(db, now);
            var ownRows = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel(),
                userA.Id,
                onlyOwnHistory: true);
            Assert.Single(ownRows.Items);
            Assert.Equal(userA.UserCode, ownRows.Items[0].SchoolId);

            var otherBorrowerSearch = await service.GetHistoryAsync(
                new BorrowingHistoryFilterViewModel { Search = "STUDENT-B" },
                userA.Id,
                onlyOwnHistory: true);
            Assert.Empty(otherBorrowerSearch.Items);

            var stream = new List<BorrowingHistoryRowViewModel>();
            await foreach (var row in service.StreamReportRowsAsync(
                new BorrowingHistoryFilterViewModel(),
                userA.Id,
                onlyOwnHistory: true))
            {
                stream.Add(row);
            }

            Assert.Single(stream);
            Assert.Equal(userA.UserCode, stream[0].SchoolId);
        }

        [Fact]
        public async Task History_is_paginated_and_clamps_out_of_range_page_numbers()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var now = Utc(2026, 10, 14, 4);
            var user = DashboardSeed.User(db, "STUDENT-A");
            var profile = DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "CAM-1");

            for (var index = 0; index < 12; index++)
            {
                var start = now.AddDays(index);
                DashboardSeed.Reservation(
                    db,
                    profile,
                    item,
                    start,
                    start.AddHours(1),
                    DomainValues.ReservationStatuses.Pending);
            }

            var secondPage = await Service(db, now).GetHistoryAsync(
                new BorrowingHistoryFilterViewModel { Page = 2 },
                user.Id,
                onlyOwnHistory: false);
            var firstPage = await Service(db, now).GetHistoryAsync(
                new BorrowingHistoryFilterViewModel(), user.Id, onlyOwnHistory: false);
            Assert.Equal(10, firstPage.Items.Count);
            Assert.Equal(12, secondPage.TotalCount);
            Assert.Equal(2, secondPage.TotalPages);
            Assert.Equal(2, secondPage.Items.Count);

            var clampedPage = await Service(db, now).GetHistoryAsync(
                new BorrowingHistoryFilterViewModel { Page = 1000 },
                user.Id,
                onlyOwnHistory: false);
            Assert.Equal(2, clampedPage.Page);
            Assert.Equal(2, clampedPage.Items.Count);
        }

        [Fact]
        public async Task Invalid_date_range_is_rejected_without_querying_records()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "STUDENT-A");
            var model = await Service(db, Utc(2026, 10, 14, 4)).GetHistoryAsync(
                new BorrowingHistoryFilterViewModel
                {
                    FromDate = new DateTime(2026, 10, 15),
                    ToDate = new DateTime(2026, 10, 14)
                },
                user.Id,
                onlyOwnHistory: false);

            Assert.Equal("The start date must be on or before the end date.", model.ValidationMessage);
            Assert.Empty(model.Items);
            Assert.Equal(0, model.TotalCount);
        }

        private static BorrowingHistoryService Service(
            AsiBasecodeDBContext db,
            DateTime nowUtc) => new(new UnitOfWork(db), new FixedTimeProvider(nowUtc));

        private static DateTime Utc(int year, int month, int day, int hour) =>
            new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

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
