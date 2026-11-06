using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Issues and verifies single-use password reset one-time passcodes.
    /// </summary>
    public interface IPasswordResetOtpService
    {
        /// <summary>
        /// Reserves rate-limit budget, generates and hashes a fresh OTP, stores it,
        /// and emails it to the user. If the email dispatch fails, the stored OTP is
        /// rolled back so a future attempt starts clean.
        /// </summary>
        Task IssueOtpAsync(
            ApplicationUser user,
            string fallbackEmail,
            CancellationToken cancellationToken = default);

        Task<PasswordResetOtpIssuanceResult> TryReserveIssuanceAsync(
            ApplicationUser user,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        Task<PasswordResetOtpAttemptResult> TryReserveAttemptAsync(
            string userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        Task<IdentityResult> SetAsync(
            ApplicationUser user,
            string value);

        Task<bool> TryConsumeAsync(
            string userId,
            string expectedValue,
            CancellationToken cancellationToken = default);
    }
}
