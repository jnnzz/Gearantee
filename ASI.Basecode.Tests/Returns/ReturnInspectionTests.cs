using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Utilities;
using System;
using Xunit;

namespace ASI.Basecode.Tests.Returns
{
    public sealed class ReturnInspectionTests
    {
        private static readonly DateTime Due = new(2026, 10, 17, 4, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData(-60, 0)]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(60 * 24, 1)]
        [InlineData(60 * 24 + 1, 2)]
        public void Days_late_rounds_partial_days_up(int minutesAfterDue, int expected)
        {
            Assert.Equal(expected, ReturnInspection.DaysLate(Due, Due.AddMinutes(minutesAfterDue)));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(15, false)]
        [InlineData(16, true)]
        [InlineData(60 * 24, true)]
        public void Backdated_means_more_than_the_grace_minutes_before_recording(int minutesBeforeRecording, bool expected)
        {
            Assert.Equal(expected, ReturnInspection.IsBackdated(Due.AddMinutes(-minutesBeforeRecording), Due));
        }

        [Theory]
        [InlineData(DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available, true)]
        [InlineData(DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.UnderMaintenance, true)]
        [InlineData(DomainValues.ReturnConditions.Damaged, DomainValues.EquipmentStatuses.Available, false)]
        [InlineData(DomainValues.ReturnConditions.Damaged, DomainValues.EquipmentStatuses.Unavailable, true)]
        [InlineData(DomainValues.ReturnConditions.NeedsInspection, DomainValues.EquipmentStatuses.Available, false)]
        [InlineData(DomainValues.ReturnConditions.UnderRepair, DomainValues.EquipmentStatuses.Available, false)]
        public void Only_good_items_may_become_available(string condition, string status, bool expected)
        {
            Assert.Equal(expected, ReturnInspection.IsAllowed(condition, status));
        }

        [Theory]
        [InlineData(DomainValues.ReturnConditions.Good, DomainValues.EquipmentStatuses.Available)]
        [InlineData(DomainValues.ReturnConditions.Damaged, DomainValues.EquipmentStatuses.UnderMaintenance)]
        [InlineData(DomainValues.ReturnConditions.NeedsInspection, DomainValues.EquipmentStatuses.UnderMaintenance)]
        public void Default_status_follows_condition(string condition, string expected)
        {
            Assert.Equal(expected, ReturnInspection.DefaultStatusFor(condition));
        }

        [Theory]
        [InlineData("NeedsInspection", "Needs Inspection")]
        [InlineData("UnderMaintenance", "Under Maintenance")]
        [InlineData("Good", "Good")]
        public void Label_splits_pascal_case(string value, string expected)
        {
            Assert.Equal(expected, ReturnInspection.Label(value));
        }
    }
}
