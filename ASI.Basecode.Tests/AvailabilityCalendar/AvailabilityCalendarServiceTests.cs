using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.AvailabilityCalendar;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.AvailabilityCalendar
{
    public sealed class AvailabilityCalendarServiceTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 14, 0);

        [Fact]
        public void Calendar_uses_shared_reservation_rules_and_hides_borrower_details()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var borrower = DashboardSeed.Profile(db, DashboardSeed.User(db, "PRIVATE-BORROWER"));
            var reservedItem = DashboardSeed.Item(db, "RESERVED");
            var pendingItem = DashboardSeed.Item(db, "PENDING");
            var borrowedItem = DashboardSeed.Item(db, "BORROWED", DomainValues.EquipmentStatuses.Borrowed);
            var overdueItem = DashboardSeed.Item(db, "OVERDUE", DomainValues.EquipmentStatuses.Borrowed);
            var returnedItem = DashboardSeed.Item(db, "RETURNED");
            var maintenanceItem = DashboardSeed.Item(db, "MAINT", DomainValues.EquipmentStatuses.UnderMaintenance);
            var archivedItem = DashboardSeed.Item(db, "ARCHIVED");
            archivedItem.IsArchived = true;
            var inactiveCategoryItem = DashboardSeed.Item(db, "INACTIVE-CATEGORY", categoryName: "Retired");
            DashboardSeed.Category(db, "Retired").IsActive = false;

            var reserved = DashboardSeed.Reservation(db, borrower, reservedItem,
                Now.AddDays(1), Now.AddDays(1).AddHours(2), DomainValues.ReservationStatuses.Approved);
            reserved.Purpose = "Private purpose value";
            DashboardSeed.Reservation(db, borrower, pendingItem,
                Now.AddDays(1), Now.AddDays(1).AddHours(2), DomainValues.ReservationStatuses.Pending);
            var borrowed = DashboardSeed.Reservation(db, borrower, borrowedItem,
                Now.AddDays(2), Now.AddDays(2).AddHours(2), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, borrowed, Now.AddDays(2));
            var overdue = DashboardSeed.Reservation(db, borrower, overdueItem,
                Now.AddDays(-2), Now.AddDays(-1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, overdue, Now.AddDays(-2));
            var returned = DashboardSeed.Reservation(db, borrower, returnedItem,
                Now.AddDays(-2), Now.AddDays(-1), DomainValues.ReservationStatuses.Approved);
            var returnedRelease = DashboardSeed.Release(db, returned, Now.AddDays(-2));
            DashboardSeed.Return(db, returnedRelease, Now.AddDays(-1));
            db.SaveChanges();

            var end = Now.AddDays(7);
            var data = Calendar(db).GetCalendarData(
                new AvailabilityCalendarFilterViewModel(),
                new DateTimeOffset(Now),
                new DateTimeOffset(end));

            Assert.Equal(new[] { "BORROWED", "OVERDUE", "RESERVED" },
                data.Events.Select(item => item.Title.Split(" Â· ")[0]).OrderBy(code => code));
            Assert.Equal(new[] { "Borrowed", "Overdue", "Reserved" },
                data.Events.Select(item => item.Status).OrderBy(status => status));
            Assert.Equal(Now, data.Events.Single(item => item.Status == "Overdue").Start.UtcDateTime);
            Assert.Equal(end, data.Events.Single(item => item.Status == "Overdue").End.UtcDateTime);
            Assert.Contains(data.Items, item => item.ItemCode == "MAINT" && !item.CanBeReserved &&
                item.AvailabilitySummary.Contains("no return date recorded", StringComparison.Ordinal));
            Assert.DoesNotContain(data.Items, item => item.ItemCode == "ARCHIVED" || item.ItemCode == "INACTIVE-CATEGORY");
            Assert.Equal("Available throughout this period",
                data.Items.Single(item => item.ItemCode == "PENDING").AvailabilitySummary);

            var json = JsonSerializer.Serialize(data);
            Assert.DoesNotContain("PRIVATE-BORROWER", json);
            Assert.DoesNotContain("Private purpose value", json);
        }

        [Fact]
        public void Category_item_and_location_filters_are_combined()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var mainRoom = DashboardSeed.Item(db, "LAPTOP");
            var labRoom = DashboardSeed.Item(db, "PROJECTOR", categoryName: "Projectors");
            labRoom.Location = "Lab 2";
            db.SaveChanges();

            var data = Calendar(db).GetCalendarData(
                new AvailabilityCalendarFilterViewModel
                {
                    CategoryId = mainRoom.CategoryId,
                    EquipmentId = mainRoom.EquipmentId,
                    Location = "Main Room"
                },
                new DateTimeOffset(Now),
                new DateTimeOffset(Now.AddDays(1)));

            var item = Assert.Single(data.Items);
            Assert.Equal("LAPTOP", item.ItemCode);
            Assert.Empty(data.Events);
        }

        private static AvailabilityCalendarService Calendar(ASI.Basecode.Data.AsiBasecodeDBContext db)
        {
            var unitOfWork = new ASI.Basecode.Data.UnitOfWork(db);
            return new AvailabilityCalendarService(
                new ASI.Basecode.Data.Repositories.EquipmentItemRepository(unitOfWork),
                new ASI.Basecode.Data.Repositories.ReservationRepository(unitOfWork),
                new FixedTimeProvider(Now));
        }
    }
}
