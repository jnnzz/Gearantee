using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public sealed class IdentityPasswordResetOtpStore : IPasswordResetOtpStore
    {
        private const string Provider = "Gearantee";
        private const string TokenName = "PasswordResetOtp";
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
