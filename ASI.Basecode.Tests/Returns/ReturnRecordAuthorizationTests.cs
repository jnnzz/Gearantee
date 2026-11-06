using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Authorization;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Returns
{
    /// <summary>Evaluates the combined [Authorize] policy for each role and permission.</summary>
    public sealed class ReturnRecordAuthorizationTests
    {
        private const string ReleaseReturn = DomainValues.Permissions.TransactionReleaseReturn;
        private const string Browse = DomainValues.Permissions.EquipmentBrowse;

        [Theory]
        [InlineData(nameof(ReturnRecordController.Index), false)]
        [InlineData(nameof(ReturnRecordController.Details), false)]
        [InlineData(nameof(ReturnRecordController.Create), false)]
        [InlineData(nameof(ReturnRecordController.Create), true)]
        public async Task Custodian_and_administrator_with_the_grant_are_allowed(string action, bool post)
        {
            Assert.True(await CanAccess(action, post, DomainValues.Roles.Custodian, ReleaseReturn));
            Assert.True(await CanAccess(action, post, DomainValues.Roles.Administrator, ReleaseReturn));
        }

        [Theory]
        [InlineData(nameof(ReturnRecordController.Index), false)]
        [InlineData(nameof(ReturnRecordController.Create), true)]
        public async Task Borrower_or_missing_grant_is_denied(string action, bool post)
        {
            Assert.False(await CanAccess(action, post, DomainValues.Roles.Borrower, ReleaseReturn));
            Assert.False(await CanAccess(action, post, DomainValues.Roles.Custodian, Browse));
            Assert.False(await CanAccess(action, post, DomainValues.Roles.Administrator));
        }

        [Fact]
        public void Every_return_post_validates_the_antiforgery_token()
        {
            var posts = typeof(ReturnRecordController).GetMethods()
                .Where(method => method.GetCustomAttributes<HttpPostAttribute>(true).Any())
                .ToList();

            Assert.NotEmpty(posts);
            Assert.All(posts, method => Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>(true)));
        }

        private static async Task<bool> CanAccess(string action, bool post, string role, params string[] permissions)
        {
            var controller = typeof(ReturnRecordController);
            var method = controller.GetMethods().Single(candidate =>
                candidate.Name == action &&
                candidate.GetCustomAttributes<HttpPostAttribute>(true).Any() == post);
            var authorizeData = controller.GetCustomAttributes<AuthorizeAttribute>(true)
                .Concat(method.GetCustomAttributes<AuthorizeAttribute>(true))
                .ToList();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization(options =>
            {
                // Same shape as Startup.Auth: one policy per permission claim
                foreach (var permission in new[] { ReleaseReturn, Browse })
                {
                    options.AddPolicy(permission, policy =>
                        policy.RequireClaim(ApplicationClaimsPrincipalFactory.PermissionClaimType, permission));
                }
            });
            using var provider = services.BuildServiceProvider();

            var policy = await AuthorizationPolicy.CombineAsync(
                provider.GetRequiredService<IAuthorizationPolicyProvider>(), authorizeData);
            Assert.NotNull(policy);

            var claims = permissions
                .Select(permission => new Claim(ApplicationClaimsPrincipalFactory.PermissionClaimType, permission))
                .Append(new Claim(ClaimTypes.Role, role));
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

            var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);
            return result.Succeeded;
        }
    }
}
