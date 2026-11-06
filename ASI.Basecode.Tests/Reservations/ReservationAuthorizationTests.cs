using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Authorization;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Reservations
{
    /// <summary>
    /// Evaluates the real combined [Authorize] policy (controller + action) for each role, so
    /// these tests check who is allowed in, not just which attributes are present.
    /// </summary>
    public sealed class ReservationAuthorizationTests
    {
        private const string Borrower = DomainValues.Roles.Borrower;
        private const string Custodian = DomainValues.Roles.Custodian;
        private const string Administrator = DomainValues.Roles.Administrator;
        private const string Browse = DomainValues.Permissions.EquipmentBrowse;
        private const string Create = DomainValues.Permissions.ReservationCreate;

        [Theory]
        [InlineData(Borrower)]
        [InlineData(Custodian)]
        [InlineData(Administrator)]
        public async Task Any_role_with_browse_permission_can_view_the_catalog(string role)
        {
            Assert.True(await CanAccess(typeof(CatalogController), nameof(CatalogController.Index), false, role, Browse));
            Assert.True(await CanAccess(typeof(CatalogController), nameof(CatalogController.Details), false, role, Browse));
        }

        [Theory]
        [InlineData(Borrower)]
        [InlineData(Custodian)]
        [InlineData(Administrator)]
        public async Task Catalog_is_denied_without_browse_permission(string role)
        {
            Assert.False(await CanAccess(typeof(CatalogController), nameof(CatalogController.Index), false, role, Create));
            Assert.False(await CanAccess(typeof(CatalogController), nameof(CatalogController.Details), false, role));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Only_a_borrower_with_create_permission_can_submit(bool post)
        {
            var create = nameof(ReservationController.Create);

            Assert.True(await CanAccess(typeof(ReservationController), create, post, Borrower, Browse, Create));
            Assert.False(await CanAccess(typeof(ReservationController), create, post, Borrower, Browse));
            Assert.False(await CanAccess(typeof(ReservationController), create, post, Custodian, Browse, Create));
            Assert.False(await CanAccess(typeof(ReservationController), create, post, Administrator, Browse, Create));
        }

        [Theory]
        [InlineData(nameof(ReservationController.Index), false)]
        [InlineData(nameof(ReservationController.Details), false)]
        [InlineData(nameof(ReservationController.Cancel), true)]
        public async Task Borrower_keeps_own_history_without_create_permission(string action, bool post)
        {
            Assert.True(await CanAccess(typeof(ReservationController), action, post, Borrower));
            Assert.False(await CanAccess(typeof(ReservationController), action, post, Custodian, Browse));
            Assert.False(await CanAccess(typeof(ReservationController), action, post, Administrator, Browse));
        }

        [Fact]
        public void Every_reservation_post_validates_the_antiforgery_token()
        {
            var posts = typeof(ReservationController).GetMethods()
                .Where(method => method.GetCustomAttributes<HttpPostAttribute>(true).Any())
                .ToList();

            Assert.NotEmpty(posts);
            Assert.All(posts, method => Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>(true)));
        }

        private static async Task<bool> CanAccess(Type controller, string action, bool post, string role, params string[] permissions)
        {
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
                // Same shape as Startup.Auth: one policy per permission claim.
                foreach (var permission in new[] { Browse, Create })
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
