using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public sealed class PasswordResetOtpService
    {
        public const string RequestRateLimitPolicyName =
            "password-reset-request";

        public const string VerifyRateLimitPolicyName =
            "password-reset-verify";

        public const int LifetimeMinutes = 10;
        public const int MaximumAttempts = 5;
        public const int MaximumIssuancesPerHour = 5;
        public static readonly TimeSpan ResendCooldown =
            TimeSpan.FromMinutes(1);
        public static readonly TimeSpan IssuanceWindow =
            TimeSpan.FromHours(1);

        private readonly IPasswordResetOtpStore _store;
        private readonly ILogger<PasswordResetOtpService> _logger;

        public PasswordResetOtpService(
            IPasswordResetOtpStore store,
            ILogger<PasswordResetOtpService> logger = null)
        {
            _store = store;
            _logger = logger;
        }

        public Task<string> GetAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return _store.GetAsync(userId, cancellationToken);
        }

        public Task<IdentityResult> SetAsync(
            ApplicationUser user,
            string value)
        {
            return _store.SetAsync(user, value);
        }

        public async Task<PasswordResetOtpIssuanceResult> TryReserveIssuanceAsync(
            ApplicationUser user,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var currentValue = await _store.GetRateLimitAsync(
                user.Id,
                cancellationToken);
            var hasCurrentState = PasswordResetOtpRateLimitState.TryParse(
                currentValue,
                out var currentState);

            if (hasCurrentState &&
                now - currentState.LastIssuedAt < ResendCooldown)
            {
                return PasswordResetOtpIssuanceResult.Rejected(
                    PasswordResetOtpIssuanceStatus.Cooldown);
            }

            var windowStart = now;
            var issuanceCount = 1;
            if (hasCurrentState &&
                now - currentState.WindowStartAt < IssuanceWindow)
            {
                windowStart = currentState.WindowStartAt;
                if (currentState.IssuanceCount >= MaximumIssuancesPerHour)
                {
                    return PasswordResetOtpIssuanceResult.Rejected(
                        PasswordResetOtpIssuanceStatus.LimitReached);
                }

                issuanceCount = currentState.IssuanceCount + 1;
            }

            var nextState = new PasswordResetOtpRateLimitState(
                windowStart,
                issuanceCount,
                now);
            var nextValue = nextState.Serialize();
            bool reserved;
            if (hasCurrentState)
            {
                reserved = await _store.TryReplaceRateLimitAsync(
                    user.Id,
                    currentValue,
                    nextValue,
                    cancellationToken);
            }
            else
            {
                var setResult = await _store.SetRateLimitAsync(
                    user,
                    nextValue);
                reserved = setResult.Succeeded;
            }

            return reserved
                ? PasswordResetOtpIssuanceResult.Reserved(
                    windowStart,
                    issuanceCount)
                : PasswordResetOtpIssuanceResult.Rejected(
                    PasswordResetOtpIssuanceStatus.ConcurrencyConflict);
        }

        public async Task<PasswordResetOtpAttemptResult> TryReserveAttemptAsync(
            string userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var storedValue = await _store.GetAsync(
                userId,
                cancellationToken);
            if (!PasswordResetOtpState.TryParse(
                    storedValue,
                    out var state))
            {
                await RemoveIfCurrentAsync(
                    userId,
                    storedValue,
                    cancellationToken);
                return PasswordResetOtpAttemptResult.Rejected(
                    PasswordResetOtpAttemptStatus.MissingOrInvalid);
            }

            if (state.ExpiresAt <= now)
            {
                await RemoveIfCurrentAsync(
                    userId,
                    storedValue,
                    cancellationToken);
                return PasswordResetOtpAttemptResult.Rejected(
                    PasswordResetOtpAttemptStatus.Expired);
            }

            if (state.Attempts >= MaximumAttempts)
            {
                await RemoveIfCurrentAsync(
                    userId,
                    storedValue,
                    cancellationToken);
                return PasswordResetOtpAttemptResult.Rejected(
                    PasswordResetOtpAttemptStatus.TooManyAttempts);
            }

            var reservedState = state.WithAttempts(state.Attempts + 1);
            var reservedValue = reservedState.Serialize();
            var reserved = await _store.TryReplaceAsync(
                userId,
                storedValue,
                reservedValue,
                cancellationToken);
            if (!reserved)
            {
                return PasswordResetOtpAttemptResult.Rejected(
                    PasswordResetOtpAttemptStatus.ConcurrencyConflict);
            }

            return PasswordResetOtpAttemptResult.Reserved(
                reservedState,
                reservedValue);
        }

        public Task<bool> TryConsumeAsync(
            string userId,
            string expectedValue,
            CancellationToken cancellationToken = default)
        {
            return _store.TryRemoveAsync(
                userId,
                expectedValue,
                cancellationToken);
        }

        private async Task RemoveIfCurrentAsync(
            string userId,
            string storedValue,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(storedValue))
            {
                var removed = await _store.TryRemoveAsync(
                    userId,
                    storedValue,
                    cancellationToken);
                if (!removed)
                {
                    _logger?.LogDebug(
                        "The password reset OTP cleanup skipped a token that had already changed for user {UserId}.",
                        userId);
                }
            }
        }
    }
}
