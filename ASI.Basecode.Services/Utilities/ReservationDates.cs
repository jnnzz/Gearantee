using System;
using System.Globalization;

namespace ASI.Basecode.Services.Utilities
{
    public static class ReservationDates
    {
        public static string StartLabel(DateTime local, bool fullDay) =>
            local.ToString(fullDay ? "ddd d MMM yyyy" : "ddd d MMM yyyy · HH:mm", CultureInfo.InvariantCulture);

        public static string EndLabel(DateTime local, bool fullDay) =>
            StartLabel(fullDay ? local.AddDays(-1) : local, fullDay);
    }
}
