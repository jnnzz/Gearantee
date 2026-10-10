using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IPasswordResetOtpStore
    {
        Task<string> GetAsync(
            string userId,
            CancellationToken cancellationToken = default);

        Task<IdentityResult> SetAsync(
            ApplicationUser user,
            string value);

        Task<string> GetRateLimitAsync(
            string userId,
            CancellationToken cancellationToken = default);

        Task<IdentityResult> SetRateLimitAsync(
            ApplicationUser user,
            string value);

        Task<bool> TryReplaceRateLimitAsync(
            string userId,
            string expectedValue,
            string replacementValue,
            CancellationToken cancellationToken = default);

        Task<bool> TryReplaceAsync(
            string userId,
            string expectedValue,
            string replacementValue,
            CancellationToken cancellationToken = default);

        Task<bool> TryRemoveAsync(
            string userId,
            string expectedValue,
            CancellationToken cancellationToken = default);
    }
}
