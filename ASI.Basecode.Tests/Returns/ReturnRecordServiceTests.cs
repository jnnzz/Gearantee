using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels.Returns;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using System;
using System.Linq;
using Xunit;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;
using static ASI.Basecode.Tests.Returns.ReturnTestHelpers;

namespace ASI.Basecode.Tests.Returns
{
    public sealed class ReturnRecordServiceTests
    {
        private static readonly DateTime Now = Utc(2026, 10, 17, 4);

        [Theory]
        [InlineData(ReturnRecordService.OnLoanTab)]
        [InlineData(ReturnRecordService.OverdueTab)]
        [InlineData(ReturnRecordService.ReturnedTab)]
        [InlineData(ReturnRecordService.LateTab)]
        public void Each_return_tab_limits_pages_to_ten_records(string tab)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-PAGES", DomainValues.Roles.Custodian);
            var service = ReturnsFor(db, Now);
            for (var index = 0; index < 11; index++)
            {
                var release = Loan(db, $"PAGE-{index:00}", Now.AddDays(-2), Now.AddHours(-1), Now.AddDays(-2));
                if (tab == ReturnRecordService.ReturnedTab || tab == ReturnRecordService.LateTab)
                {
                    var result = service.AddReturnRecord(
                        Form(release, Now, DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), custodian.Id);
                    Assert.True(result.Succeeded, result.Message);
                }
            }
            var first = service.GetReturnRecords(tab, 1);
            var last = service.GetReturnRecords(tab, 99);
            Assert.Equal(11, first.TotalCount);
            Assert.Equal(2, first.TotalPages);
            Assert.Equal(10, first.Items.Count);
            Assert.Equal(2, last.Page);
            Assert.Single(last.Items);
            Assert.Empty(first.Items.Select(item => item.ReservationCode).Intersect(last.Items.Select(item => item.ReservationCode)));
        }

        private static ReturnRecordViewModel Form(ReleaseRecord release, DateTime returnedUtc, string condition, string status, string notes = null) => new()
        {
            ReleaseRecordId = release.ReleaseRecordId,
            ActualReturnAt = ManilaClock.ToLocal(returnedUtc),
            ReturnedCondition = condition,
            ResultingItemStatus = status,
            Notes = notes
        };

        private static EquipmentItem ItemFor(SqliteDashboardDbContext db, ReleaseRecord release)
        {
            db.ChangeTracker.Clear();
            return db.ReleaseRecords
                .Where(item => item.ReleaseRecordId == release.ReleaseRecordId)
                .Select(item => item.Reservation.EquipmentItem)
                .Single();
        }

        [Fact]
        public void On_time_good_return_records_receiver_and_frees_item()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now.AddMinutes(-5), DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available, "  All parts present  "),
                custodian.Id);

            Assert.True(result.Succeeded, result.Message);
            db.ChangeTracker.Clear();
            var saved = db.ReturnRecords.Single();
            Assert.Equal(result.ReturnRecordId, saved.ReturnRecordId);
            Assert.Equal(custodian.Id, saved.ReceivedByUserId);
            Assert.Equal(Now.AddMinutes(-5), saved.ActualReturnAt);
            Assert.Equal(DomainValues.ReturnConditions.Good, saved.ReturnedCondition);
            Assert.Equal(DomainValues.EquipmentStatuses.Available, saved.ResultingItemStatus);
            Assert.Equal("All parts present", saved.Notes);
            Assert.Empty(db.LateReturns);

            var item = ItemFor(db, release);
            Assert.Equal(DomainValues.ReturnConditions.Good, item.ConditionStatus);
            Assert.Equal(DomainValues.EquipmentStatuses.Available, item.ItemStatus);
            Assert.Equal(Now, item.UpdatedAt);
        }

        [Theory]
        [InlineData(DomainValues.ReturnConditions.Damaged, DomainValues.EquipmentStatuses.UnderMaintenance)]
        [InlineData(DomainValues.ReturnConditions.NeedsInspection, DomainValues.EquipmentStatuses.Unavailable)]
        [InlineData(DomainValues.ReturnConditions.UnderRepair, DomainValues.EquipmentStatuses.UnderMaintenance)]
        [InlineData(DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Unavailable)]
        public void Condition_and_status_are_written_to_the_item(string condition, string status)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(Form(release, Now, condition, status), custodian.Id);

            Assert.True(result.Succeeded, result.Message);
            var item = ItemFor(db, release);
            Assert.Equal(condition, item.ConditionStatus);
            Assert.Equal(status, item.ItemStatus);
        }

        [Theory]
        [InlineData(DomainValues.ReturnConditions.Damaged)]
        [InlineData(DomainValues.ReturnConditions.NeedsInspection)]
        [InlineData(DomainValues.ReturnConditions.UnderRepair)]
        public void Damaged_or_uncertain_items_cannot_be_made_available(string condition)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now, condition, DomainValues.EquipmentStatuses.Available), custodian.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("Good condition", result.Message);
            db.ChangeTracker.Clear();
            Assert.Empty(db.ReturnRecords);
            Assert.Equal(DomainValues.EquipmentStatuses.Borrowed, ItemFor(db, release).ItemStatus);
        }

        [Fact]
        public void Values_are_matched_case_insensitively()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(Form(release, Now, "damaged", "undermaintenance"), custodian.Id);

            Assert.True(result.Succeeded, result.Message);
            db.ChangeTracker.Clear();
            Assert.Equal(DomainValues.ReturnConditions.Damaged, db.ReturnRecords.Single().ReturnedCondition);
            Assert.Equal(DomainValues.EquipmentStatuses.UnderMaintenance, db.ReturnRecords.Single().ResultingItemStatus);
        }

        [Theory]
        [InlineData("Broken", DomainValues.EquipmentStatuses.Available)]
        [InlineData(DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Borrowed)]
        [InlineData(DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Archived)]
        [InlineData("", DomainValues.EquipmentStatuses.Available)]
        public void Unknown_condition_or_status_is_refused(string condition, string status)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(Form(release, Now, condition, status), custodian.Id);

            Assert.False(result.Succeeded);
            db.ChangeTracker.Clear();
            Assert.Empty(db.ReturnRecords);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(60 * 24, 1)]
        [InlineData(60 * 24 + 1, 2)]
        [InlineData(60 * 50, 3)]
        public void Late_return_logs_whole_days_late(int minutesLate, int expectedDays)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var due = Now.AddMinutes(-minutesLate);
            var release = Loan(db, "ITEM-1", due.AddDays(-1), due, due.AddDays(-1));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now, DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), custodian.Id);

            Assert.True(result.Succeeded, result.Message);
            Assert.Contains("late", result.Message);
            db.ChangeTracker.Clear();
            var late = db.LateReturns.Single();
            Assert.Equal(db.ReturnRecords.Single().ReturnRecordId, late.ReturnRecordId);
            Assert.Equal(due, late.DueAt);
            Assert.Equal(Now, late.ReturnedAt);
            Assert.Equal(expectedDays, late.DaysLate);
            Assert.Equal(DomainValues.PenaltyStatuses.Recorded, late.PenaltyStatus);
        }

        [Fact]
        public void Return_exactly_at_due_time_is_not_late()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-1), Now, Now.AddDays(-1));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now, DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), custodian.Id);

            Assert.True(result.Succeeded, result.Message);
            db.ChangeTracker.Clear();
            Assert.Empty(db.LateReturns);
        }

        [Fact]
        public void A_release_can_only_be_returned_once()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));
            var service = ReturnsFor(db, Now);
            var form = Form(release, Now, DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available);

            Assert.True(service.AddReturnRecord(form, custodian.Id).Succeeded);
            db.ChangeTracker.Clear();
            var second = service.AddReturnRecord(form, custodian.Id);

            Assert.False(second.Succeeded);
            Assert.Contains("already been returned", second.Message);
            db.ChangeTracker.Clear();
            Assert.Single(db.ReturnRecords);
        }

        [Fact]
        public void Return_time_in_the_future_is_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now.AddMinutes(1), DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), custodian.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("future", result.Message);
        }

        [Fact]
        public void Return_time_before_release_is_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-1));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now.AddDays(-1).AddMinutes(-1), DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), custodian.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("before the item was released", result.Message);
        }

        [Fact]
        public void Backdated_return_without_a_note_is_refused()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(-3), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now.AddHours(-4), DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available, "   "), custodian.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("explaining why", result.Message);
            db.ChangeTracker.Clear();
            Assert.Empty(db.ReturnRecords);
            Assert.Equal(DomainValues.EquipmentStatuses.Borrowed, ItemFor(db, release).ItemStatus);
        }

        [Fact]
        public void Backdated_return_with_a_note_is_saved_and_flagged()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(-3), Now.AddDays(-2));
            var service = ReturnsFor(db, Now);

            var result = service.AddReturnRecord(
                Form(release, Now.AddHours(-4), DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available, "Handed back at lunch"), custodian.Id);

            Assert.True(result.Succeeded, result.Message);
            db.ChangeTracker.Clear();
            Assert.Empty(db.LateReturns);
            var details = service.RetrieveReturnRecord(result.ReturnRecordId.Value);
            Assert.True(details.IsBackdated);
            Assert.Equal(ManilaClock.ToLocal(Now), details.RecordedAtLocal);
            Assert.Equal(ManilaClock.ToLocal(Now.AddHours(-4)), details.ReturnedAtLocal);
        }

        [Fact]
        public void Return_within_the_grace_window_needs_no_note()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));
            var service = ReturnsFor(db, Now);

            var result = service.AddReturnRecord(
                Form(release, Now.AddMinutes(-ReturnInspection.BackdateGraceMinutes), DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), custodian.Id);

            Assert.True(result.Succeeded, result.Message);
            db.ChangeTracker.Clear();
            Assert.False(service.RetrieveReturnRecord(result.ReturnRecordId.Value).IsBackdated);
        }

        [Fact]
        public void Unknown_release_is_not_found()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);

            var result = ReturnsFor(db, Now).AddReturnRecord(new ReturnRecordViewModel
            {
                ReleaseRecordId = 999,
                ActualReturnAt = ManilaClock.ToLocal(Now),
                ReturnedCondition = DomainValues.ReturnConditions.Good,
                ResultingItemStatus = DomainValues.EquipmentStatuses.Available
            }, custodian.Id);

            Assert.True(result.NotFound);
        }

        [Fact]
        public void Administrator_with_the_grant_can_record_returns()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var admin = Staff(db, "ADM-1", DomainValues.Roles.Administrator);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now, DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), admin.Id);

            Assert.True(result.Succeeded, result.Message);
        }

        [Theory]
        [InlineData(DomainValues.Roles.Custodian, false, true)]
        [InlineData(DomainValues.Roles.Custodian, true, false)]
        [InlineData(DomainValues.Roles.Borrower, true, true)]
        public void Revoked_inactive_or_wrong_role_users_cannot_record_returns(string role, bool granted, bool active)
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var user = Staff(db, "USR-1", role, granted: granted, active: active);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));

            var result = ReturnsFor(db, Now).AddReturnRecord(
                Form(release, Now, DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), user.Id);

            Assert.False(result.Succeeded);
            Assert.Contains("permission", result.Message);
            db.ChangeTracker.Clear();
            Assert.Empty(db.ReturnRecords);
        }

        [Fact]
        public void Tabs_split_loans_overdue_returned_and_late()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            Loan(db, "ON-TIME", Now.AddDays(-1), Now.AddDays(1), Now.AddDays(-1));
            Loan(db, "OVERDUE", Now.AddDays(-3), Now.AddDays(-1), Now.AddDays(-3));
            var returned = Loan(db, "RETURNED", Now.AddDays(-3), Now.AddDays(-2), Now.AddDays(-3));
            var late = Loan(db, "LATE", Now.AddDays(-5), Now.AddDays(-4), Now.AddDays(-5));
            var service = ReturnsFor(db, Now);
            Assert.True(service.AddReturnRecord(Form(returned, Now.AddDays(-2).AddHours(-1), DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available, "Logged late"), custodian.Id).Succeeded);
            Assert.True(service.AddReturnRecord(Form(late, Now.AddDays(-2), DomainValues.ReturnConditions.Damaged, DomainValues.EquipmentStatuses.UnderMaintenance, "Logged late"), custodian.Id).Succeeded);
            db.ChangeTracker.Clear();

            var onLoan = service.GetReturnRecords(null, 1);

            Assert.Equal(ReturnRecordService.OnLoanTab, onLoan.Tab);
            Assert.Equal(new[] { 2, 1, 2, 1 }, onLoan.Tabs.Select(tab => tab.Count));
            Assert.Equal(new[] { "OVERDUE", "ON-TIME" }, onLoan.Items.Select(item => item.ItemCode));
            Assert.Equal(new[] { "Overdue", "Borrowed" }, onLoan.Items.Select(item => item.DisplayStatus));

            var overdue = service.GetReturnRecords("overdue", 1);
            Assert.Equal("OVERDUE", Assert.Single(overdue.Items).ItemCode);

            var returns = service.GetReturnRecords(ReturnRecordService.ReturnedTab, 1);
            Assert.Equal(new[] { "LATE", "RETURNED" }, returns.Items.Select(item => item.ItemCode));
            Assert.All(returns.Items, item => Assert.Equal("Returned", item.DisplayStatus));

            var lateTab = service.GetReturnRecords(ReturnRecordService.LateTab, 1);
            var lateItem = Assert.Single(lateTab.Items);
            Assert.Equal("LATE", lateItem.ItemCode);
            Assert.Equal(2, lateItem.DaysLate);
            Assert.Equal(DomainValues.ReturnConditions.Damaged, lateItem.ReturnedCondition);
        }

        [Fact]
        public void Unknown_tab_falls_back_to_on_loan()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();

            var model = ReturnsFor(db, Now).GetReturnRecords("nope", 5);

            Assert.Equal(ReturnRecordService.OnLoanTab, model.Tab);
            Assert.Equal(1, model.Page);
            Assert.Empty(model.Items);
        }

        [Fact]
        public void Active_loan_is_gone_once_returned()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-2), Now.AddHours(2), Now.AddDays(-2));
            var service = ReturnsFor(db, Now);

            var loan = service.RetrieveActiveLoan(release.ReleaseRecordId);
            Assert.NotNull(loan);
            Assert.Equal("BRW-ITEM-1 Test", loan.BorrowerName);
            Assert.Equal(ManilaClock.ToLocal(Now.AddHours(2)), loan.DueAtLocal);

            service.AddReturnRecord(Form(release, Now, DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available), custodian.Id);
            db.ChangeTracker.Clear();

            Assert.Null(service.RetrieveActiveLoan(release.ReleaseRecordId));
        }

        [Fact]
        public void Details_show_receiver_notes_and_late_penalty()
        {
            using var fixture = new SqliteDb();
            using var db = fixture.Create();
            var custodian = Staff(db, "CUS-1", DomainValues.Roles.Custodian);
            var release = Loan(db, "ITEM-1", Now.AddDays(-3), Now.AddDays(-1), Now.AddDays(-3));
            var service = ReturnsFor(db, Now);
            var result = service.AddReturnRecord(
                Form(release, Now, DomainValues.ReturnConditions.NeedsInspection, DomainValues.EquipmentStatuses.UnderMaintenance, "Lens cap missing"),
                custodian.Id);
            db.ChangeTracker.Clear();

            var details = service.RetrieveReturnRecord(result.ReturnRecordId.Value);

            Assert.Equal("CUS-1 Test", details.ReceivedByName);
            Assert.Equal("Lens cap missing", details.Notes);
            Assert.Equal(DomainValues.EquipmentStatuses.UnderMaintenance, details.ResultingItemStatus);
            Assert.Equal(1, details.DaysLate);
            Assert.Equal(DomainValues.PenaltyStatuses.Recorded, details.PenaltyStatus);
            Assert.Null(service.RetrieveReturnRecord(999));
        }
    }
}
