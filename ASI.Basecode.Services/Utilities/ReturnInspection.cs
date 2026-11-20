using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ASI.Basecode.Services.Utilities
{
    /// <summary>Condition-check rules for a returned item (WBS 45.00-47.00).</summary>
    public static class ReturnInspection
    {
        public static readonly IReadOnlyList<string> Conditions = new[]
        {
            DomainValues.ReturnConditions.Good,
            DomainValues.ReturnConditions.Damaged,
            DomainValues.ReturnConditions.NeedsInspection,
            DomainValues.ReturnConditions.UnderRepair
        };

        public static readonly IReadOnlyList<string> ResultingStatuses = new[]
        {
            DomainValues.EquipmentStatuses.Available,
            DomainValues.EquipmentStatuses.UnderMaintenance,
            DomainValues.EquipmentStatuses.Unavailable
        };

        // Damaged or uncertain items are never made available automatically
        public static bool IsAllowed(string condition, string status)
        {
            // Cataraja: WBS 45-46: shared inspection rule used by the return service.
            // Cataraja: Damaged/uncertain items cannot be marked Available by a tampered form.
            return status != DomainValues.EquipmentStatuses.Available ||
                condition == DomainValues.ReturnConditions.Good;
        }

        public static string DefaultStatusFor(string condition)
        {
            return condition == DomainValues.ReturnConditions.Good
                ? DomainValues.EquipmentStatuses.Available
                : DomainValues.EquipmentStatuses.UnderMaintenance;
        }

        public const int BackdateGraceMinutes = 15;

        // Dated well before it was recorded; needs a reason note
        public static bool IsBackdated(DateTime returnedUtc, DateTime recordedUtc)
        {
            return returnedUtc < recordedUtc.AddMinutes(-BackdateGraceMinutes);
        }

        // Any lateness counts; partial days round up
        public static int DaysLate(DateTime dueUtc, DateTime returnedUtc)
        {
            // Cataraja: WBS 47: exactly on time is zero; any later instant counts at
            // Cataraja: least one day. Compare UTC instants to avoid local-time display ambiguity.
            if (returnedUtc <= dueUtc)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Ceiling((returnedUtc - dueUtc).TotalDays));
        }

        // Matches a posted value to its canonical spelling, or null
        public static string Normalize(IEnumerable<string> allowed, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return allowed.FirstOrDefault(option => string.Equals(option, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // NeedsInspection -> Needs Inspection
        public static string Label(string value)
        {
            return string.IsNullOrEmpty(value) ? value : Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");
        }
    }
}
