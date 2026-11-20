using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data
{
    // WBS 10–11: stores the hashed OTP state and issuance limits in
    // ASP.NET Identity's AspNetUserTokens table for the reset service to verify.
    public sealed class IdentityPasswordResetOtpStore : IPasswordResetOtpStore
    {
        private const string Provider = "Gearantee";
        private const string TokenName = "PasswordResetOtp";
        // Issuance limits survive consuming TokenName, so a verified code cannot be
        // used to reset its own cooldown. The service reads these two states separately.
        private const string RateLimitTokenName = "PasswordResetOtpRateLimit";

        private readonly AsiBasecodeDBContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public IdentityPasswordResetOtpStore(
            AsiBasecodeDBContext dbContext,
            UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public async Task<string> GetAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext
                .Set<IdentityUserToken<string>>()
                .AsNoTracking()
                .Where(token =>
                    token.UserId == userId &&
                    token.LoginProvider == Provider &&
                    token.Name == TokenName)
                .Select(token => token.Value)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public Task<IdentityResult> SetAsync(
            ApplicationUser user,
            string value)
        {
            return _userManager.SetAuthenticationTokenAsync(
                user,
                Provider,
                TokenName,
                value);
        }

        public async Task<string> GetRateLimitAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return await GetTokenValueAsync(
                userId,
                RateLimitTokenName,
                cancellationToken);
        }

        public Task<IdentityResult> SetRateLimitAsync(
            ApplicationUser user,
            string value)
        {
            return _userManager.SetAuthenticationTokenAsync(
                user,
                Provider,
                RateLimitTokenName,
                value);
        }

        public async Task<bool> TryReplaceAsync(
            string userId,
            string expectedValue,
            string replacementValue,
            CancellationToken cancellationToken = default)
        {
            // WBS 11: one SQL update both checks the expected state and saves the new
            // attempt count. A stale competing request affects zero rows and returns false.
            var rowsAffected = await _dbContext
                .Set<IdentityUserToken<string>>()
                .Where(token =>
                    token.UserId == userId &&
                    token.LoginProvider == Provider &&
                    token.Name == TokenName &&
                    token.Value == expectedValue)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        token => token.Value,
                        replacementValue),
                    cancellationToken);

            return rowsAffected == 1;
        }

        public async Task<bool> TryReplaceRateLimitAsync(
            string userId,
            string expectedValue,
            string replacementValue,
            CancellationToken cancellationToken = default)
        {
            var rowsAffected = await _dbContext
                .Set<IdentityUserToken<string>>()
                .Where(token =>
                    token.UserId == userId &&
                    token.LoginProvider == Provider &&
                    token.Name == RateLimitTokenName &&
                    token.Value == expectedValue)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        token => token.Value,
                        replacementValue),
                    cancellationToken);

            return rowsAffected == 1;
        }

        public async Task<bool> TryRemoveAsync(
            string userId,
            string expectedValue,
            CancellationToken cancellationToken = default)
        {
            // One conditional delete consumes a code once. Include expectedValue so
            // an older verifier/failed-email cleanup cannot remove a newly issued code.
            var rowsAffected = await _dbContext
                .Set<IdentityUserToken<string>>()
                .Where(token =>
                    token.UserId == userId &&
                    token.LoginProvider == Provider &&
                    token.Name == TokenName &&
                    token.Value == expectedValue)
                .ExecuteDeleteAsync(cancellationToken);

            return rowsAffected == 1;
        }

        private async Task<string> GetTokenValueAsync(
            string userId,
            string tokenName,
            CancellationToken cancellationToken)
        {
            return await _dbContext
                .Set<IdentityUserToken<string>>()
                .AsNoTracking()
                .Where(token =>
                    token.UserId == userId &&
                    token.LoginProvider == Provider &&
                    token.Name == tokenName)
                .Select(token => token.Value)
                .SingleOrDefaultAsync(cancellationToken);
        }
    }
}
