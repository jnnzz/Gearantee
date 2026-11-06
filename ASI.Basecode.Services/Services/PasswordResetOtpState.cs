using System;
using System.Globalization;

namespace ASI.Basecode.Services.Services
{
    public sealed class PasswordResetOtpState
    {
        private const string CurrentVersion = "v2";

        public PasswordResetOtpState(
            DateTimeOffset expiresAt,
            int attempts,
            DateTimeOffset issuanceWindowStartAt,
            int issuanceCount,
            DateTimeOffset lastIssuedAt,
            string otpHash)
        {
            ExpiresAt = expiresAt;
            Attempts = attempts;
            IssuanceWindowStartAt = issuanceWindowStartAt;
            IssuanceCount = issuanceCount;
            LastIssuedAt = lastIssuedAt;
            OtpHash = otpHash;
        }

        public DateTimeOffset ExpiresAt { get; }

        public int Attempts { get; }

        public DateTimeOffset IssuanceWindowStartAt { get; }

        public int IssuanceCount { get; }

        public DateTimeOffset LastIssuedAt { get; }

        public string OtpHash { get; }

        public PasswordResetOtpState WithAttempts(int attempts)
        {
            return new PasswordResetOtpState(
                ExpiresAt,
                attempts,
                IssuanceWindowStartAt,
                IssuanceCount,
                LastIssuedAt,
                OtpHash);
        }

        public string Serialize()
        {
            return string.Join(
                "|",
                CurrentVersion,
                ExpiresAt.ToUnixTimeSeconds()
                    .ToString(CultureInfo.InvariantCulture),
                Attempts.ToString(CultureInfo.InvariantCulture),
                IssuanceWindowStartAt.ToUnixTimeSeconds()
                    .ToString(CultureInfo.InvariantCulture),
                IssuanceCount.ToString(CultureInfo.InvariantCulture),
                LastIssuedAt.ToUnixTimeSeconds()
                    .ToString(CultureInfo.InvariantCulture),
                OtpHash);
        }

        public static bool TryParse(
            string storedOtp,
            out PasswordResetOtpState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(storedOtp))
            {
                return false;
            }

            var parts = storedOtp.Split('|');
            try
            {
                if (parts.Length == 7 && parts[0] == CurrentVersion)
                {
                    if (!TryParseLong(parts[1], out var expirySeconds) ||
                        !TryParseInt(parts[2], out var attempts) ||
                        !TryParseLong(parts[3], out var windowSeconds) ||
                        !TryParseInt(parts[4], out var issuanceCount) ||
                        !TryParseLong(parts[5], out var lastIssuedSeconds) ||
                        attempts < 0 ||
                        issuanceCount < 1 ||
                        string.IsNullOrWhiteSpace(parts[6]))
                    {
                        return false;
                    }

                    state = new PasswordResetOtpState(
                        DateTimeOffset.FromUnixTimeSeconds(expirySeconds),
                        attempts,
                        DateTimeOffset.FromUnixTimeSeconds(windowSeconds),
                        issuanceCount,
                        DateTimeOffset.FromUnixTimeSeconds(lastIssuedSeconds),
                        parts[6]);
                    return true;
                }

                // Accept the original three-part format so an OTP issued
                // before this deployment can still be completed safely.
                if (parts.Length != 3 ||
                    !TryParseLong(parts[0], out var legacyExpirySeconds) ||
                    !TryParseInt(parts[1], out var legacyAttempts) ||
                    legacyAttempts < 0 ||
                    string.IsNullOrWhiteSpace(parts[2]))
                {
                    return false;
                }

                var legacyExpiry = DateTimeOffset.FromUnixTimeSeconds(
                    legacyExpirySeconds);
                var legacyIssuedAt = legacyExpiry.AddMinutes(-10);
                state = new PasswordResetOtpState(
                    legacyExpiry,
                    legacyAttempts,
                    legacyIssuedAt,
                    1,
                    legacyIssuedAt,
                    parts[2]);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                state = null;
                return false;
            }
        }

        private static bool TryParseLong(string value, out long result)
        {
            return long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static bool TryParseInt(string value, out int result)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
