using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.Resources.Constants;
using System;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    public class FullDayReservationTests
    {
        private static readonly DateTime Now = Utc(2026, 11, 6, 3);

        [Theory]
        [InlineData(7, 7, 1)]
        [InlineData(7, 9, 3)]
        public void Dates_cover_every_selected_day_and_ignore_posted_times(int start, int end, int days)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "DAY-BORROWER");
            DashboardSeed.Profile(db, user);
            var item = DashboardSeed.Item(db, "DAY-ITEM");
            var service = ReservationsFor(db, Now);
            var result = service.SubmitReservation(new ReservationCreateViewModel {
                EquipmentId = item.EquipmentId, Purpose = "Class",
                ReservationStart = new DateTime(2026, 11, start, 18, 30, 0),
                ReservationEnd = new DateTime(2026, 11, end, 1, 0, 0)
            }, user.Id);
            Assert.True(result.Succeeded, result.Message);
            var saved = db.Reservations.Single();
            Assert.True(saved.IsFullDay);
            Assert.Equal(ManilaClock.ToUtc(new DateTime(2026, 11, start)), saved.ReservationStart);
            Assert.Equal(saved.ReservationStart.AddDays(days), saved.ReservationEnd);
            var detail = service.RetrieveMyReservation(saved.ReservationId, user.Id);
            Assert.Equal(new DateTime(2026, 11, end).ToString("ddd d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), detail.EndLabel);
        }

        [Fact]
        public void Old_timed_records_keep_their_time_and_full_day_due_today_uses_inclusive_end_date()
        {
            var due = ManilaClock.ToUtc(new DateTime(2026, 11, 7));
            Assert.Equal("Due today", ReservationDisplayStatus.For("Approved", due, Now.AddDays(-1), null, Now, true));
            Assert.Equal("Borrowed", ReservationDisplayStatus.For("Approved", due, Now.AddDays(-1), null, Now));
            Assert.Contains("14:30", ReservationDates.EndLabel(new DateTime(2026, 11, 7, 14, 30, 0), false));
            Assert.NotNull(ReservationAvailability.ValidateDates(DateTime.MaxValue, DateTime.MaxValue, Now));
            Assert.NotNull(ReservationAvailability.ValidateDates(new DateTime(2026, 11, 6), new DateTime(2026, 11, 6), Now));
        }

        [Fact]
        public void Search_stays_scoped_to_borrower_and_combines_with_status_and_paging()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = DashboardSeed.User(db, "SEARCH-ME");
            var mine = DashboardSeed.Profile(db, user);
            var other = DashboardSeed.Profile(db, DashboardSeed.User(db, "SEARCH-OTHER"));
            var item = DashboardSeed.Item(db, "LAP-001-ASU");
            var own = DashboardSeed.Reservation(db, mine, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var hidden = DashboardSeed.Reservation(db, other, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);
            var service = ReservationsFor(db, Now);
            var results = service.GetMyReservations(user.Id, "Pending", 99, "lap-001");
            Assert.Equal(own.ReservationId, Assert.Single(results.Items).ReservationId);
            Assert.Equal(1, results.Page);
            Assert.Equal(1, results.Tabs.First().Count);
            Assert.Empty(service.GetMyReservations(user.Id, null, 1, $"RSV-{hidden.ReservationId}").Items);
            Assert.Single(service.GetMyReservations(user.Id, null, 1, $"RSV-{own.ReservationId}").Items);
            Assert.Empty(service.GetMyReservations(user.Id, "Rejected", 1, "LAP").Items);
        }
    }
}
