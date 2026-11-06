using System;

namespace ASI.Basecode.Services.Utilities
{
    public static class ManilaClock
    {
        private static readonly TimeZoneInfo ManilaTimeZone =
            ResolveManilaTimeZone();

        public static DateTime NowLocal => ToLocal(DateTime.UtcNow);

        private static TimeZoneInfo ResolveManilaTimeZone()
        {
            foreach (var id in new[] { "Asia/Manila", "Singapore Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            // The Philippines does not observe daylight saving time, so UTC+8
            // remains a correct fallback on systems with no installed zone data.
            return TimeZoneInfo.CreateCustomTimeZone(
                "Gearantee/Manila",
                TimeSpan.FromHours(8),
                "Philippine Time",
                "Philippine Time");
        }

        public static DateTime ToLocal(DateTime utc)
        {
            var utcValue = utc.Kind == DateTimeKind.Utc
                ? utc
                : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeFromUtc(utcValue, ManilaTimeZone),
                DateTimeKind.Unspecified);
        }

        public static DateTime ToUtc(DateTime local)
        {
            return DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
                    ManilaTimeZone),
                DateTimeKind.Utc);
        }

        public static (DateTime StartUtc, DateTime EndUtc) TodayUtcRange()
        {
            return TodayUtcRange(DateTime.UtcNow);
        }

        public static (DateTime StartUtc, DateTime EndUtc) TodayUtcRange(
            DateTime nowUtc)
        {
            var todayLocal = ToLocal(nowUtc).Date;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(todayLocal, DateTimeKind.Unspecified),
                ManilaTimeZone);
            return (startUtc, startUtc.AddDays(1));
        }

        public static (DateTime StartUtc, DateTime EndUtc) CurrentMonthUtcRange()
        {
            var nowLocal = NowLocal;
            var startLocal = new DateTime(
                nowLocal.Year,
                nowLocal.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Unspecified);
            var nextLocal = startLocal.AddMonths(1);
            return (
                TimeZoneInfo.ConvertTimeToUtc(startLocal, ManilaTimeZone),
                TimeZoneInfo.ConvertTimeToUtc(nextLocal, ManilaTimeZone));
        }
    }
}
