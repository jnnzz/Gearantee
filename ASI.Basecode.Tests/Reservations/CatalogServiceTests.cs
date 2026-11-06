using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Catalog;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Reservations
{
    public sealed class CatalogServiceTests
    {
        private static readonly System.DateTime Now = Utc(2026, 10, 7, 0);

        [Fact]
        public void Catalog_lists_only_reservable_items()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            DashboardSeed.Item(db, "OK-AVAILABLE");
            DashboardSeed.Item(db, "OK-BORROWED", DomainValues.EquipmentStatuses.Borrowed);
            DashboardSeed.Item(db, "NO-MAINT", DomainValues.EquipmentStatuses.UnderMaintenance);
            DashboardSeed.Item(db, "NO-UNAVAIL", DomainValues.EquipmentStatuses.Unavailable);
            DashboardSeed.Item(db, "NO-ARCHIVED").IsArchived = true;
            DashboardSeed.Item(db, "NO-CATEGORY", categoryName: "Retired");
            DashboardSeed.Category(db, "Retired").IsActive = false;
            db.SaveChanges();

            var model = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel());

            Assert.Equal(new[] { "OK-AVAILABLE", "OK-BORROWED" }, model.Items.Select(item => item.ItemCode).OrderBy(code => code));
            Assert.Equal(2, model.TotalCount);
            Assert.DoesNotContain(model.Categories, category => category.CategoryName == "Retired");
        }

        [Fact]
        public void Search_matches_code_name_brand_and_model_and_category_filters()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var laptop = DashboardSeed.Item(db, "LAP-001");
            laptop.ItemName = "ThinkPad L14";
            laptop.Brand = "Lenovo";
            var projector = DashboardSeed.Item(db, "PROJ-001", categoryName: "Projectors");
            projector.ItemName = "Projector";
            projector.Model = "EB-X51";
            db.SaveChanges();
            var service = Catalog(db, Now);

            Assert.Equal("LAP-001", service.GetCatalog(new CatalogFilterViewModel { Search = "Lenovo" }).Items.Single().ItemCode);
            Assert.Equal("LAP-001", service.GetCatalog(new CatalogFilterViewModel { Search = "ThinkPad" }).Items.Single().ItemCode);
            Assert.Equal("PROJ-001", service.GetCatalog(new CatalogFilterViewModel { Search = "EB-X" }).Items.Single().ItemCode);
            Assert.Equal("PROJ-001", service.GetCatalog(new CatalogFilterViewModel { Search = " PROJ-001 " }).Items.Single().ItemCode);
            Assert.Equal("PROJ-001", service.GetCatalog(new CatalogFilterViewModel { CategoryId = projector.CategoryId }).Items.Single().ItemCode);
            Assert.Empty(service.GetCatalog(new CatalogFilterViewModel { Search = "nothing-like-this" }).Items);
        }

        [Fact]
        public void Window_filter_hides_items_booked_in_that_window()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var booked = DashboardSeed.Item(db, "BOOKED");
            DashboardSeed.Item(db, "FREE");
            var pendingOnly = DashboardSeed.Item(db, "PENDING-ONLY");
            DashboardSeed.Reservation(db, profile, booked, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Reservation(db, profile, pendingOnly, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Pending);

            var model = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel
            {
                From = ManilaClock.ToLocal(Now.AddDays(1).AddHours(2)),
                To = ManilaClock.ToLocal(Now.AddDays(1).AddHours(4))
            });

            Assert.Null(model.WindowError);
            Assert.Equal(new[] { "FREE", "PENDING-ONLY" }, model.Items.Select(item => item.ItemCode).OrderBy(code => code));
        }

        [Theory]
        [InlineData(2, 1)]   // end before start
        [InlineData(-2, 1)]  // start in the past
        public void Invalid_window_reports_an_error_and_is_ignored(int startOffsetHours, int lengthHours)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var booked = DashboardSeed.Item(db, "BOOKED");
            DashboardSeed.Reservation(db, profile, booked, Now.AddDays(-1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);

            var start = ManilaClock.ToLocal(Now.AddHours(startOffsetHours));
            var end = startOffsetHours > 0 ? start.AddHours(-lengthHours) : start.AddHours(lengthHours);
            var model = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel { From = start, To = end });

            Assert.NotNull(model.WindowError);
            Assert.Single(model.Items);
        }

        [Fact]
        public void Only_one_side_of_the_window_reports_an_error()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();

            var model = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel { From = ManilaClock.ToLocal(Now.AddDays(1)) });

            Assert.NotNull(model.WindowError);
        }

        [Fact]
        public void Item_on_active_loan_shows_as_borrowed()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "LOANED", DomainValues.EquipmentStatuses.Borrowed);
            var reservation = DashboardSeed.Reservation(db, profile, item, Now.AddDays(-1), Now.AddDays(1), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Release(db, reservation, Now.AddDays(-1));
            DashboardSeed.Item(db, "SHELF");

            var items = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel()).Items;

            Assert.Equal(DomainValues.EquipmentStatuses.Borrowed, items.Single(i => i.ItemCode == "LOANED").AvailabilityStatus);
            Assert.Equal(DomainValues.EquipmentStatuses.Available, items.Single(i => i.ItemCode == "SHELF").AvailabilityStatus);
        }

        [Fact]
        public void Paging_clamps_to_the_last_page()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            for (var i = 0; i < 21; i++)
            {
                DashboardSeed.Item(db, $"ITEM-{i:00}");
            }

            var model = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel { Page = 99 });

            var first = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel());
            var second = Catalog(db, Now).GetCatalog(new CatalogFilterViewModel { Page = 2 });
            Assert.Equal(10, first.Items.Count);
            Assert.Equal(10, second.Items.Count);
            Assert.Empty(first.Items.Select(item => item.EquipmentId).Intersect(second.Items.Select(item => item.EquipmentId)));
            Assert.Equal(3, model.TotalPages);
            Assert.Equal(3, model.Page);
            Assert.Single(model.Items);
        }

        [Fact]
        public void Details_lists_upcoming_bookings_and_hides_unreservable_items()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var profile = DashboardSeed.Profile(db, DashboardSeed.User(db, "BRW-1"));
            var item = DashboardSeed.Item(db, "ITEM-1");
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(3), Now.AddDays(4), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(1), Now.AddDays(2), DomainValues.ReservationStatuses.Approved);
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(5), Now.AddDays(6), DomainValues.ReservationStatuses.Pending);
            DashboardSeed.Reservation(db, profile, item, Now.AddDays(-3), Now.AddDays(-2), DomainValues.ReservationStatuses.Approved);
            var maintenance = DashboardSeed.Item(db, "ITEM-2", DomainValues.EquipmentStatuses.UnderMaintenance);
            var service = Catalog(db, Now);

            var details = service.RetrieveCatalogItem(item.EquipmentId);

            Assert.Equal(2, details.BookedWindows.Count);
            Assert.Equal(ManilaClock.ToLocal(Now.AddDays(1)), details.BookedWindows[0].StartLocal);
            Assert.All(details.BookedWindows, window => Assert.Equal("Reserved", window.Label));
            Assert.Null(service.RetrieveCatalogItem(maintenance.EquipmentId));
            Assert.Null(service.RetrieveCatalogItem(999));
        }
    }
}
