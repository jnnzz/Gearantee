using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using System;

namespace ASI.Basecode.Services.Utilities
{
    /// <summary>
    /// The label a borrower sees for a reservation. Mirrors the dashboard's labels
    /// (DashboardService.GetDisplayStatus) so both screens agree: once an approved
    /// reservation is released, the loan state (Borrowed, Due today, Overdue, Returned)
    /// replaces the stored status.
    /// </summary>
    public static class ReservationDisplayStatus
    {
        public static string For(
            string status,
            DateTime endUtc,
            DateTime? releasedAtUtc,
            DateTime? returnedAtUtc,
            DateTime nowUtc)
        {
            if (returnedAtUtc.HasValue)
            {
                return "Returned";
            }

            if (releasedAtUtc.HasValue)
            {
                if (endUtc < nowUtc)
                {
                    return "Overdue";
                }

                var (todayStart, todayEnd) = ManilaClock.TodayUtcRange(nowUtc);
                return endUtc >= todayStart && endUtc < todayEnd ? "Due today" : "Borrowed";
            }

            if (status == DomainValues.ReservationStatuses.Approved)
            {
                return endUtc < nowUtc ? "Missed pickup" : "Awaiting Release";
            }

            return status;
        }
    }
}
