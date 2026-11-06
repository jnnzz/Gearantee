using System;
using System.Globalization;

namespace ASI.Basecode.Services.Services
{
    public sealed class PasswordResetOtpRateLimitState
    {
        public PasswordResetOtpRateLimitState(
            DateTimeOffset windowStartAt,
            int issuanceCount,
            DateTimeOffset lastIssuedAt)
        {
            WindowStartAt = windowStartAt;
            IssuanceCount = issuanceCount;
            LastIssuedAt = lastIssuedAt;
        }

        public DateTimeOffset WindowStartAt { get; }

        public int IssuanceCount { get; }

        public DateTimeOffset LastIssuedAt { get; }

        public string Serialize()
        {
            return string.Join(
                "|",
                WindowStartAt.ToUnixTimeSeconds()
                    .ToString(CultureInfo.InvariantCulture),
                IssuanceCount.ToString(CultureInfo.InvariantCulture),
                LastIssuedAt.ToUnixTimeSeconds()
                    .ToString(CultureInfo.InvariantCulture));
        }

        public static bool TryParse(
            string storedValue,
            out PasswordResetOtpRateLimitState state)
        {
            state = null;
            var parts = storedValue?.Split('|');
            if (parts == null ||
                parts.Length != 3 ||
                !long.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var windowSeconds) ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var issuanceCount) ||
                !long.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var lastIssuedSeconds) ||
                issuanceCount < 1)
            {
                return false;
            }

            try
            {
                state = new PasswordResetOtpRateLimitState(
                    DateTimeOffset.FromUnixTimeSeconds(windowSeconds),
                    issuanceCount,
                    DateTimeOffset.FromUnixTimeSeconds(lastIssuedSeconds));
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                state = null;
                return false;
            }
        }
    }
}
