using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ASI.Basecode.Tests.BorrowerProfiles
{
    /// <summary>
    /// WBS 30.00-32.00 authorization surface. Mirrors CategoryAuthorizationTests so the
    /// borrower-profile screens carry the same guards as the other management screens.
    /// </summary>
    public class BorrowerProfileAuthorizationTests
    {
        [Fact]
        public void BorrowerProfileController_RequiresAdministratorAndBorrowerManagePermission()
        {
            var authorization = typeof(BorrowerProfileController)
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Single();

            Assert.Equal(DomainValues.Roles.Administrator, authorization.Roles);
            Assert.Equal(DomainValues.Permissions.BorrowerManage, authorization.Policy);
        }

        [Fact]
        public void BorrowerProfileController_DisablesResponseCaching()
        {
            var cache = typeof(BorrowerProfileController)
                .GetCustomAttribute<ResponseCacheAttribute>();

            Assert.NotNull(cache);
            Assert.True(cache.NoStore);
            Assert.Equal(ResponseCacheLocation.None, cache.Location);
        }

        [Theory]
        [InlineData(nameof(BorrowerProfileController.Edit))]
        [InlineData(nameof(BorrowerProfileController.UpdateEligibility))]
        public void MutatingActions_RequireAntiforgery(string actionName)
        {
            var method = typeof(BorrowerProfileController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == actionName &&
                    candidate.GetCustomAttribute<HttpPostAttribute>() != null);

            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }

        [Theory]
        [InlineData(nameof(BorrowerProfileController.Index))]
        [InlineData(nameof(BorrowerProfileController.Edit))]
        public void ReadActions_AreGetOnlyOnTheReadOverload(string actionName)
        {
            var getOverloads = typeof(BorrowerProfileController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(candidate => candidate.Name == actionName &&
                    candidate.GetCustomAttribute<HttpGetAttribute>() != null)
                .ToList();

            Assert.Single(getOverloads);
            Assert.Null(getOverloads[0].GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }
}
