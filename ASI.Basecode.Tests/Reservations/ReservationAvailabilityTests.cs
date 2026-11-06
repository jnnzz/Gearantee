using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    public sealed class ReservationAvailabilityTests
    {
        private static readonly System.DateTime Now = Utc(2026, 10, 7, 0);

        [Theory]
        [InlineData(DomainValues.ReservationStatuses.Pending, false)]
        [InlineData(DomainValues.ReservationStatuses.Approved, true)]
        [InlineData(DomainValues.ReservationStatuses.Rejected, false)]
        [InlineData(DomainValues.ReservationStatuses.Cancelled, false)]
        [InlineData(DomainValues.ReservationStatuses.Expired, false)]
        public void Only_approved_reservations_block_an_overlapping_window(string status, bool blocks)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), status);

            var blocked = db.Reservations
                .Where(ReservationAvailability.Blocks(Now.AddDays(1).AddHours(6), Now.AddDays(3), Now))
                .Any();

            Assert.Equal(blocks, blocked);
        }

        [Fact]
        public void Windows_that_only_touch_do_not_conflict()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);

            Assert.False(db.Reservations.Any(ReservationAvailability.Blocks(Now.AddDays(2), Now.AddDays(3), Now)));
            Assert.False(db.Reservations.Any(ReservationAvailability.Blocks(Now.AddHours(12), Now.AddDays(1), Now)));
        }

        [Fact]
        public void Returned_loan_no_longer_blocks_its_window()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            var release = DashboardSeed.Release(db, reservation, Now.AddDays(-1));
            DashboardSeed.Return(db, release, Now.AddHours(-1));

            Assert.False(db.Reservations.Any(ReservationAvailability.Blocks(Now.AddDays(1), Now.AddDays(3), Now)));
        }

        [Fact]
        public void Overdue_active_loan_blocks_every_future_window()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1", DomainValues.EquipmentStatuses.Borrowed);
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-3), Now.AddDays(-1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, reservation, Now.AddDays(-3));

            Assert.True(db.Reservations.Any(ReservationAvailability.Blocks(Now.AddDays(10), Now.AddDays(11), Now)));
        }

        [Fact]
        public void On_time_active_loan_blocks_only_its_own_window()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1", DomainValues.EquipmentStatuses.Borrowed);
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, reservation, Now.AddDays(-1));

            Assert.True(db.Reservations.Any(ReservationAvailability.Blocks(Now.AddHours(1), Now.AddDays(2), Now)));
            Assert.False(db.Reservations.Any(ReservationAvailability.Blocks(Now.AddDays(1), Now.AddDays(2), Now)));
        }
    }
}
