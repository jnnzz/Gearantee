using System;

namespace ASI.Basecode.Services.Services
{
    public enum PasswordResetOtpAttemptStatus
    {
        Reserved,
        MissingOrInvalid,
        Expired,
        TooManyAttempts,
        ConcurrencyConflict
    }

    public enum PasswordResetOtpIssuanceStatus
    {
        Reserved,
        Cooldown,
        LimitReached,
        ConcurrencyConflict
    }

    public sealed class PasswordResetOtpAttemptResult
    {
        private PasswordResetOtpAttemptResult(
            PasswordResetOtpAttemptStatus status,
            PasswordResetOtpState state,
            string reservedValue)
        {
            Status = status;
            State = state;
            ReservedValue = reservedValue;
        }

        public PasswordResetOtpAttemptStatus Status { get; }

        public PasswordResetOtpState State { get; }

        public string ReservedValue { get; }

        public bool IsReserved =>
            Status == PasswordResetOtpAttemptStatus.Reserved;

        public static PasswordResetOtpAttemptResult Reserved(
            PasswordResetOtpState state,
            string reservedValue)
        {
            return new PasswordResetOtpAttemptResult(
                PasswordResetOtpAttemptStatus.Reserved,
                state,
                reservedValue);
        }

        public static PasswordResetOtpAttemptResult Rejected(
            PasswordResetOtpAttemptStatus status)
        {
            return new PasswordResetOtpAttemptResult(status, null, null);
        }
    }

    public sealed class PasswordResetOtpIssuanceResult
    {
        private PasswordResetOtpIssuanceResult(
            PasswordResetOtpIssuanceStatus status,
            DateTimeOffset windowStartAt,
            int issuanceCount)
        {
            Status = status;
            WindowStartAt = windowStartAt;
            IssuanceCount = issuanceCount;
        }

        public PasswordResetOtpIssuanceStatus Status { get; }

        public DateTimeOffset WindowStartAt { get; }

        public int IssuanceCount { get; }

        public bool IsReserved =>
            Status == PasswordResetOtpIssuanceStatus.Reserved;

        public static PasswordResetOtpIssuanceResult Reserved(
            DateTimeOffset windowStartAt,
            int issuanceCount)
        {
            return new PasswordResetOtpIssuanceResult(
                PasswordResetOtpIssuanceStatus.Reserved,
                windowStartAt,
                issuanceCount);
        }

        public static PasswordResetOtpIssuanceResult Rejected(
            PasswordResetOtpIssuanceStatus status)
        {
            return new PasswordResetOtpIssuanceResult(status, default, 0);
        }
    }
}
